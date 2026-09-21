// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AttachmentDownloaderTests
    {
        [Fact]
        public async Task DownloadFilesAsync_DisallowedHost_SkipsAttachment_NoOutboundRequest()
        {
            var handler = new RecordingHandler();
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = new AttachmentDownloader(factory, validator);
            var context = CreateContext("https://evil.example.com/steal");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            // Fail-closed: caller still gets a (non-null) list, the disallowed attachment is skipped,
            // and no server-side request was ever issued.
            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_AllowedHost_DownloadsAttachment()
        {
            var handler = new RecordingHandler();
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = new AttachmentDownloader(factory, validator);
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(1, handler.CallCount);
            Assert.Equal("https://files.contoso.com/doc.txt", result[0].ContentUrl);
        }

        [Fact]
        public async Task DownloadFilesAsync_ValidatorDisabled_DownloadsAnyHost()
        {
            var handler = new RecordingHandler();
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions { Enabled = false });

            var downloader = new AttachmentDownloader(factory, validator);
            var context = CreateContext("https://evil.example.com/steal");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(1, handler.CallCount);
        }

        private static TurnContext CreateContext(string contentUrl)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Webchat,
                Attachments = new List<Attachment>
                {
                    new Attachment
                    {
                        ContentType = "application/octet-stream",
                        ContentUrl = contentUrl
                    }
                }
            };

            return new TurnContext(new SimpleAdapter(), activity);
        }

        private static IHttpClientFactory CreateFactory(RecordingHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
            return factory.Object;
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("file-bytes", Encoding.UTF8, "text/plain")
                };
                return Task.FromResult(response);
            }
        }
    }
}
