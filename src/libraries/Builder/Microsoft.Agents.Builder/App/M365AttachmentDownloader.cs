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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Downloads attachments from M365/Teams using the configured Token Provider (from IConnections).
    /// </summary>
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

            string accessToken = "";

            // If authentication is enabled, get access token
            if (!_options.UseAnonymous)
            {
                IAccessTokenProvider accessTokenProvider = null;
                if (string.IsNullOrEmpty(_options.TokenProviderName))
                {
                    accessTokenProvider = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
                }
                else
                {
                    if (!_connections.TryGetConnection(_options.TokenProviderName, out accessTokenProvider))
                    {
                        accessTokenProvider = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
                    }
                }

                accessToken = await accessTokenProvider.GetAccessTokenAsync(turnContext.Identity.GetOutgoingAudience(), _options.Scopes).ConfigureAwait(false);
            }

            List<InputFile> files = [];

            foreach (Attachment attachment in attachments)
            {
                InputFile? file = await DownloadFileAsync(attachment, accessToken, cancellationToken);
                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private async Task<InputFile?> DownloadFileAsync(Attachment attachment, string accessToken, CancellationToken cancellationToken)
        {
            string? name = attachment.Name;

            if (attachment.ContentUrl != null && (attachment.ContentUrl.StartsWith("https://") || attachment.ContentUrl.StartsWith("http://localhost")))
            {
                // Get downloadable content link
                string downloadUrl;
                var contentProperties = ProtocolJsonSerializer.ToJsonElements(attachment.Content);
                if (contentProperties == null || !contentProperties.TryGetValue("downloadUrl", out System.Text.Json.JsonElement value))
                {
                    downloadUrl = attachment.ContentUrl;
                }
                else
                {
                    downloadUrl = value.ToString();
                }

                HttpResponseMessage response;
                if (_validatedHttpClient != null)
                {
                    response = await _validatedHttpClient.SendAsync(
                        new Uri(downloadUrl),
                        request => request.Headers.Add("Authorization", $"Bearer {accessToken}"),
                        cancellationToken).ConfigureAwait(false);
                    if (response == null)
                    {
                        return null;
                    }
                }
                else
                {
                    using var httpClient = _httpClientFactory.CreateClient(nameof(M365AttachmentDownloader));
                    using HttpRequestMessage request = new(HttpMethod.Get, downloadUrl);
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
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
