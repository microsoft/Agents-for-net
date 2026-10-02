// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public class AttachmentDownloader : IInputFileDownloader
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AttachmentDownloadHttpClient? _validatedHttpClient;

        public AttachmentDownloader(IHttpClientFactory httpClientFactory, IOutboundHostValidator hostValidator = null)
            : this(httpClientFactory, hostValidator, new DnsHostAddressResolver(), validatedHttpClient: null)
        {
        }

        internal AttachmentDownloader(
            IHttpClientFactory httpClientFactory,
            IOutboundHostValidator hostValidator,
            IHostAddressResolver hostAddressResolver,
            HttpClient validatedHttpClient)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _validatedHttpClient = hostValidator?.Enabled == true
                ? validatedHttpClient == null
                    ? new AttachmentDownloadHttpClient(hostValidator, hostAddressResolver)
                    : new AttachmentDownloadHttpClient(hostValidator, hostAddressResolver, validatedHttpClient)
                : null;
        }

        public async Task<IList<InputFile>> DownloadFilesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.ChannelId.IsParentChannel(Channels.Msteams))
            {
                return [];
            }

            if (turnContext.Activity.Attachments == null || turnContext.Activity.Attachments.Count == 0)
            {
                return [];
            }

            List<InputFile> files = [];

            foreach (Attachment attachment in turnContext.Activity.Attachments)
            {
                InputFile? file = await DownloadFileAsync(attachment, cancellationToken);
                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private async Task<InputFile?> DownloadFileAsync(Attachment attachment, CancellationToken cancellationToken)
        {
            string? name = attachment.Name;

            if (attachment.ContentUrl != null && (attachment.ContentUrl.StartsWith("https://") || attachment.ContentUrl.StartsWith("http://localhost")))
            {
                // Determine where the file is hosted.
                var remoteFileUrl = attachment.ContentUrl;

                HttpResponseMessage response;
                if (_validatedHttpClient != null)
                {
                    response = await _validatedHttpClient.SendAsync(
                        new Uri(remoteFileUrl),
                        configureInitialRequest: null,
                        cancellationToken).ConfigureAwait(false);
                    if (response == null)
                    {
                        return null;
                    }
                }
                else
                {
                    using var httpClient = _httpClientFactory.CreateClient(nameof(AttachmentDownloader));
                    using HttpRequestMessage request = new(HttpMethod.Get, remoteFileUrl);
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
}
