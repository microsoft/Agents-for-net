// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// </remarks>
    public class M365AttachmentDownloader : IInputFileDownloader
    {
        private readonly M365AttachmentDownloaderOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;
        private readonly AttachmentDownloadHttpClient? _validatedHttpClient;

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

        internal M365AttachmentDownloader(
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            M365AttachmentDownloaderOptions options,
            IOutboundHostValidator hostValidator,
            IHostAddressResolver hostAddressResolver,
            HttpClient validatedHttpClient)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            _options = options ?? new();
            _connections = connections;
            _httpClientFactory = httpClientFactory;
            _validatedHttpClient = hostValidator?.Enabled == true
                ? validatedHttpClient == null
                    ? new AttachmentDownloadHttpClient(hostValidator, hostAddressResolver)
                    : new AttachmentDownloadHttpClient(hostValidator, hostAddressResolver, validatedHttpClient)
                : null;
        }

        /// <inheritdoc />
        public async Task<IList<InputFile>> DownloadFilesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams && turnContext.Activity.ChannelId != Channels.M365Copilot)
            {
                return [];
            }

            // Filter out HTML attachments
            IEnumerable<Attachment>? attachments = turnContext.Activity.Attachments?.Where((a) => !a.ContentType.StartsWith(ContentTypes.Html));
            if (attachments == null || !attachments.Any())
            {
                return [];
            }

            Task<string>? accessTokenTask = null;
            List<InputFile> files = [];

            foreach (Attachment attachment in attachments)
            {
                bool preAuthorized = TryGetPreAuthorizedDownloadUrl(attachment, out string downloadUrl);
                Uri downloadUri = null;
                if (preAuthorized)
                {
                    if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps)
                    {
                        continue;
                    }
                }
                else if (attachment.ContentUrl != null &&
                    (attachment.ContentUrl.StartsWith("https://") || attachment.ContentUrl.StartsWith("http://localhost")))
                {
                    downloadUri = new Uri(attachment.ContentUrl);
                }

                string accessToken = null;
                if (downloadUri != null && !preAuthorized && !_options.UseAnonymous)
                {
                    accessTokenTask ??= GetAccessTokenAsync(turnContext);
                    accessToken = await accessTokenTask.ConfigureAwait(false);
                }

                InputFile? file = await DownloadFileAsync(attachment, downloadUri, accessToken, cancellationToken);
                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
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
            Attachment attachment, Uri downloadUri, string accessToken, CancellationToken cancellationToken)
        {
            string? name = attachment.Name;

            if (downloadUri != null)
            {
                AuthenticationHeaderValue authorization = string.IsNullOrEmpty(accessToken)
                    ? null
                    : new AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response;
                if (_validatedHttpClient != null)
                {
                    response = await _validatedHttpClient.SendAsync(
                        downloadUri,
                        request => request.Headers.Authorization = authorization,
                        cancellationToken).ConfigureAwait(false);
                    if (response == null)
                    {
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
                        return null;
                    }

                    // Convert to a buffer
#if NET8_0_OR_GREATER
                    byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
#else
                    byte[] content = await response.Content.ReadAsByteArrayAsync();
#endif

                    // Fixup content type
                    string contentType = response.Content.Headers.ContentType.MediaType;
                    if ((bool)(contentType?.StartsWith("image/")))
                    {
                        contentType = "image/png";
                    }

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
