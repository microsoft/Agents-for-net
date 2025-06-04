// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
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

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Downloads attachments from Teams using the configure Token Provider (from IConnections).
    /// </summary>
    public class TeamsAttachmentDownloader : IInputFileDownloader
    {
        private readonly TeamsAttachmentDownloaderOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAccessTokenProvider _accessTokenProvider;


        /// <summary>
        /// Creates the TeamsAttachmentDownloader
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="connections"></param>
        /// <param name="httpClientFactory"></param>
        /// <exception cref="System.ArgumentException"></exception>
        public TeamsAttachmentDownloader(TeamsAttachmentDownloaderOptions options, IConnections connections, IHttpClientFactory httpClientFactory)
        {
            AssertionHelpers.ThrowIfNull(options, nameof(options));
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            _options = options;
            if (string.IsNullOrEmpty(_options.TokenProviderName))
            {
                throw new ArgumentException("TeamsAttachmentDownloader.TokenProviderName is empty.");
            }

            _accessTokenProvider = connections.GetConnection(_options.TokenProviderName);
            if (_accessTokenProvider == null)
            {
                throw new ArgumentException("TeamsAttachmentDownloader.TokenProviderName not found.");
            }

            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public async Task<IList<InputFile>> DownloadFilesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                return [];
            }

            // Filter out HTML attachments
            IEnumerable<Attachment>? attachments = turnContext.Activity.Attachments?.Where((a) => !a.ContentType.StartsWith("text/html"));
            if (attachments == null || !attachments.Any())
            {
                return [];
            }

            string accessToken = "";

            // If authentication is enabled, get access token
            if (!_options.UseAnonymous)
            {
                accessToken = await _accessTokenProvider.GetAccessTokenAsync(AgentClaims.GetTokenAudience(turnContext.Identity), _options.Scopes).ConfigureAwait(false);
            }

            List<InputFile> files = [];

            foreach (Attachment attachment in attachments)
            {
                InputFile? file = await DownloadFileAsync(attachment, accessToken);
                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
        }


        private async Task<InputFile?> DownloadFileAsync(Attachment attachment, string accessToken)
        {
            string? name = attachment.Name;

            using var httpClient = _httpClientFactory.CreateClient(nameof(TeamsAttachmentDownloader));

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

                using HttpRequestMessage request = new(HttpMethod.Get, downloadUrl);
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

                HttpResponseMessage response = await httpClient.SendAsync(request).ConfigureAwait(false);

                // Failed to download file
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                // Convert to a buffer
                byte[] content = await response.Content.ReadAsByteArrayAsync();

                // Fixup content type
                string contentType = response.Content.Headers.ContentType.MediaType;
                if (contentType.StartsWith("image/"))
                {
                    contentType = "image/png";
                }

                return new InputFile(new BinaryData(content), contentType)
                {
                    ContentUrl = attachment.ContentUrl,
                    Filename = name
                };
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
    /// The TeamsAttachmentDownloader options
    /// </summary>
    public class TeamsAttachmentDownloaderOptions
    {
        public string TokenProviderName { get; set; }
        public bool UseAnonymous { get; set; } = false;
        public IList<string> Scopes { get; set; } = null;
    }
}
