// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Downloads attachments from M365/Teams using pre-authorized URLs or the configured token provider.
    /// </summary>
    /// <remarks>
    /// An HTTPS <c>content.downloadUrl</c> is used without a bearer token, even when <c>ContentUrl</c> is absent.
    /// Invalid pre-authorized URLs and unsuccessful HTTP responses are skipped without falling back to another URL or identity.
    /// The token provider is resolved only for authenticated <c>ContentUrl</c> downloads.
    /// Teams inline images use the current turn's connector credential and do not require an AgentExtension.
    /// </remarks>
    public class M365AttachmentDownloader : IInputFileDownloader
    {
        private readonly M365AttachmentDownloaderOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;
        private readonly AttachmentDownloadHttpClient? _validatedHttpClient;
        private readonly AttachmentDownloadHttpClient _redirectHttpClient;
        private readonly ILogger<M365AttachmentDownloader> _logger;

        /// <summary>
        /// Creates the M365AttachmentDownloader
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="connections"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="hostValidator">Optional shared allowed-hosts validator. When enabled, every download and redirect URL is validated before a request is made.</param>
        /// <exception cref="System.ArgumentException"></exception>
        public M365AttachmentDownloader(IConnections connections, IHttpClientFactory httpClientFactory, M365AttachmentDownloaderOptions options = null, IOutboundHostValidator hostValidator = null)
            : this(connections, httpClientFactory, options, hostValidator, new DnsHostAddressResolver(), validatedHttpClient: null)
        {
        }

        /// <summary>
        /// Creates an attachment downloader with diagnostic logging.
        /// </summary>
        /// <param name="connections">The configured connections.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="options">The download options.</param>
        /// <param name="hostValidator">Optional shared allowed-hosts validator.</param>
        /// <param name="logger">The diagnostic logger.</param>
        public M365AttachmentDownloader(
            IConnections connections, IHttpClientFactory httpClientFactory, M365AttachmentDownloaderOptions options,
            IOutboundHostValidator hostValidator, ILogger<M365AttachmentDownloader> logger)
            : this(connections, httpClientFactory, options, hostValidator, new DnsHostAddressResolver(), validatedHttpClient: null, logger)
        {
        }

        internal M365AttachmentDownloader(
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            M365AttachmentDownloaderOptions options,
            IOutboundHostValidator hostValidator,
            IHostAddressResolver hostAddressResolver,
            HttpClient validatedHttpClient,
            ILogger<M365AttachmentDownloader> logger = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            _options = options ?? new();
            _connections = connections;
            _httpClientFactory = httpClientFactory;
            _logger = logger ?? NullLogger<M365AttachmentDownloader>.Instance;
            _redirectHttpClient = validatedHttpClient == null
                ? new AttachmentDownloadHttpClient(hostValidator, hostAddressResolver)
                : new AttachmentDownloadHttpClient(hostValidator, hostAddressResolver, validatedHttpClient);
            _validatedHttpClient = hostValidator?.Enabled == true ? _redirectHttpClient : null;
        }

        /// <inheritdoc />
        public async Task<IList<InputFile>> DownloadFilesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams && turnContext.Activity.ChannelId != Channels.M365Copilot)
            {
                return [];
            }

            // Filter out HTML attachments
            IEnumerable<Attachment>? attachments = turnContext.Activity.Attachments?.Where(
                a => a?.ContentType != null && !a.ContentType.StartsWith(ContentTypes.Html, StringComparison.OrdinalIgnoreCase));
            if (attachments == null || !attachments.Any())
            {
                return [];
            }

            Task<string>? accessTokenTask = null;
            Task<string> GetDefaultAccessTokenAsync() => _options.UseAnonymous
                ? Task.FromResult<string>(null)
                : accessTokenTask ??= GetAccessTokenAsync(turnContext);
            List<InputFile> files = [];

            foreach (Attachment attachment in attachments)
            {
                InputFile? file = await DownloadAttachmentAsync(
                    turnContext, attachment, GetDefaultAccessTokenAsync, cancellationToken).ConfigureAwait(false);
                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private async Task<InputFile?> DownloadAttachmentAsync(
            ITurnContext turnContext, Attachment attachment, Func<Task<string>> getAccessToken, CancellationToken cancellationToken)
        {
            bool preAuthorized = TryGetPreAuthorizedDownloadUrl(attachment, out string downloadUrl);
            if (!preAuthorized && turnContext.Activity.ChannelId == Channels.Msteams
                && M365InlineImage.GetAttachmentId(attachment, out var imageUri) is string imageId)
            {
                return await DownloadInlineImageAsync(
                    turnContext, attachment, imageUri, imageId, cancellationToken).ConfigureAwait(false);
            }

            Uri downloadUri = null;
            if (preAuthorized)
            {
                if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps)
                {
                    return null;
                }
            }
            else if (attachment.ContentUrl != null &&
                (attachment.ContentUrl.StartsWith("https://") || attachment.ContentUrl.StartsWith("http://localhost")))
            {
                downloadUri = new Uri(attachment.ContentUrl);
            }

            string accessToken = downloadUri != null && !preAuthorized
                ? await getAccessToken().ConfigureAwait(false)
                : null;
            AuthenticationHeaderValue authorization = string.IsNullOrEmpty(accessToken)
                ? null
                : new AuthenticationHeaderValue("Bearer", accessToken);
            return await DownloadFileAsync(
                attachment, downloadUri, authorization, requireHttps: false, retainAuthorizationOnSameOriginRedirects: false,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<InputFile?> DownloadInlineImageAsync(
            ITurnContext turnContext, Attachment attachment, Uri uri, string id, CancellationToken cancellationToken)
        {
            if (!M365InlineImage.IsUnderServiceUrl(uri, turnContext.Activity.ServiceUrl))
            {
                _logger.LogWarning("Inline image {AttachmentId} skipped because its URL is not under the inbound HTTPS service URL.", id);
                return null;
            }

            var state = turnContext.Services.Get<InlineImageState>();
            if (state == null)
            {
                state = new InlineImageState(M365InlineImage.ReadHints(turnContext.Activity.Attachments, _logger));
                turnContext.Services.Set(state);
            }

            if (!_options.UseAnonymous && state.Authorization == null)
            {
                if (turnContext.Services.Get<IConnectorClient>() is not IRestTransport transport)
                {
                    _logger.LogWarning("Inline image {AttachmentId} skipped because no connector REST transport is available.", id);
                    return null;
                }

                cancellationToken.ThrowIfCancellationRequested();
                using var client = await transport.GetHttpClientAsync().ConfigureAwait(false);
                var authorization = client.DefaultRequestHeaders.Authorization;
                if (!string.Equals(authorization?.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(authorization.Parameter))
                {
                    _logger.LogWarning("Inline image {AttachmentId} skipped because the connector transport has no bearer credential.", id);
                    return null;
                }

                state.Authorization = authorization;
            }

            var file = await DownloadFileAsync(
                attachment, uri, _options.UseAnonymous ? null : state.Authorization,
                requireHttps: true, retainAuthorizationOnSameOriginRedirects: true, cancellationToken).ConfigureAwait(false);
            if (file != null)
            {
                M365InlineImage.SetDownloadedMetadata(file, attachment, id, state.Hints);
            }

            return file;
        }

        private Task<string> GetAccessTokenAsync(ITurnContext turnContext)
        {
            IAccessTokenProvider accessTokenProvider;
            if (string.IsNullOrEmpty(_options.TokenProviderName) ||
                !_connections.TryGetConnection(_options.TokenProviderName, out accessTokenProvider))
            {
                accessTokenProvider = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            }

            return accessTokenProvider.GetAccessTokenAsync(turnContext.Identity.GetOutgoingAudience(), _options.Scopes);
        }

        private static bool TryGetPreAuthorizedDownloadUrl(Attachment attachment, out string downloadUrl)
        {
            downloadUrl = null;
            JsonElement content;
            try
            {
                content = ProtocolJsonSerializer.ToObject<JsonElement>(attachment.Content);
            }
            catch (JsonException) when (attachment.Content is string)
            {
                return false;
            }

            if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("downloadUrl", out JsonElement value))
            {
                return false;
            }

            downloadUrl = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return true;
        }

        private async Task<InputFile?> DownloadFileAsync(
            Attachment attachment, Uri downloadUri, AuthenticationHeaderValue authorization, bool requireHttps,
            bool retainAuthorizationOnSameOriginRedirects, CancellationToken cancellationToken)
        {
            string? name = attachment.Name;

            if (downloadUri != null)
            {
                HttpResponseMessage response;
                if (_validatedHttpClient != null || requireHttps || retainAuthorizationOnSameOriginRedirects)
                {
                    response = await _redirectHttpClient.SendAsync(
                        downloadUri,
                        request => request.Headers.Authorization = authorization,
                        requireHttps,
                        retainAuthorizationOnSameOriginRedirects,
                        cancellationToken).ConfigureAwait(false);
                    if (response == null)
                    {
                        LogDownloadFailure(statusCode: null);
                        return null;
                    }
                }
                else
                {
                    using var httpClient = _httpClientFactory.CreateClient(nameof(M365AttachmentDownloader));
                    httpClient.DefaultRequestHeaders.Authorization = null;
                    using HttpRequestMessage request = new(HttpMethod.Get, downloadUri);
                    request.Headers.Authorization = authorization;
                    response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }

                using (response)
                {
                    // Failed to download file
                    if (!response.IsSuccessStatusCode)
                    {
                        LogDownloadFailure(response.StatusCode);
                        return null;
                    }

                    // Convert to a buffer
#if NET8_0_OR_GREATER
                    byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
#else
                    byte[] content = await response.Content.ReadAsByteArrayAsync();
#endif

                    string contentType = response.Content.Headers.ContentType?.MediaType
                        ?? attachment.ContentType
                        ?? "application/octet-stream";

                    return new InputFile(new BinaryData(content), contentType)
                    {
                        ContentUrl = attachment.ContentUrl,
                        Filename = name
                    };
                }
            }

            else
            {
                return new InputFile(new BinaryData(attachment.Content), attachment.ContentType)
                {
                    ContentUrl = attachment.ContentUrl,
                    Filename = name
                };
            }
        }

        private void LogDownloadFailure(HttpStatusCode? statusCode)
        {
            _logger.LogWarning(
                "Attachment download skipped; HTTP status {StatusCode} (no status means URL validation refused the request).",
                (int?)statusCode);
        }

        private sealed class InlineImageState(Dictionary<string, string> hints)
        {
            public Dictionary<string, string> Hints { get; } = hints;
            public AuthenticationHeaderValue? Authorization { get; set; }
        }
    }

    /// <summary>
    /// The M365AttachmentDownloader options
    /// </summary>
    public class M365AttachmentDownloaderOptions
    {
        public string TokenProviderName { get; set; }
        public bool UseAnonymous { get; set; } = false;
        public IList<string> Scopes { get; set; } = null;
    }
}
