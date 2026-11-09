// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class InlineAttachmentDownloaderTests
    {
        private const string ServiceUrl = "https://files.contoso.com/amer/";
        private const string ImageUrl = ServiceUrl + "v3/attachments/pic-1/views/original";
        private const string PngBytes = "89504E470D0A1A0A";

        [Theory]
        [InlineData("image/png", "png", PngBytes)]
        [InlineData("image/jpeg", "jpg", "FFD8FF0001")]
        [InlineData("image/gif", "gif", "474946383961")]
        [InlineData("image/webp", "webp", "524946460000000057454250")]
        public async Task RegisteredDownloader_InlineImage_UsesConnectorCredentialAndActualFormat(
            string contentType, string extension, string hexBytes)
        {
            byte[] bytes = FromHex(hexBytes);
            var handler = new RecordingHandler((_, _) => ImageResponse(bytes, "application/octet-stream"));
            var scenario = CreateScenario(handler, [ImageAttachment()]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            var file = Assert.Single(result);
            Assert.Equal(bytes, file.Content.ToArray());
            Assert.Equal(contentType, file.ContentType);
            Assert.Equal($"image-pic-1.{extension}", file.Filename);
            Assert.Equal(ImageUrl, file.ContentUrl);
            Assert.Equal(new[] { "Bearer connector-token" }, handler.AuthorizationHeaders);
            scenario.Transport.Verify(value => value.GetHttpClientAsync(), Times.Once);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("https://evil.example/amer/v3/attachments/pic-1/views/original")]
        [InlineData("http://files.contoso.com/amer/v3/attachments/pic-1/views/original")]
        [InlineData("https://files.contoso.com:444/amer/v3/attachments/pic-1/views/original")]
        [InlineData("https://files.contoso.com/americas/v3/attachments/pic-1/views/original")]
        [InlineData("https://files.contoso.com/amer/../other/v3/attachments/pic-1/views/original")]
        [InlineData("https://files.contoso.com/amer/%2e%2e%2fother/v3/attachments/pic-1/views/original")]
        [InlineData("https://files.contoso.com/amer/%252e%252e%252fother/v3/attachments/pic-1/views/original")]
        public async Task RegisteredDownloader_InlineImageOutsideServiceUrl_SkipsBeforeCredentials(string contentUrl)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler, [ImageAttachment(contentUrl)], validationEnabled: false);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Empty(result);
            Assert.Empty(handler.RequestUris);
            scenario.Transport.Verify(value => value.GetHttpClientAsync(), Times.Never);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RegisteredDownloader_MixedSignedAndInlineImages_DoNotShareCredentials()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler,
            [
                ImageAttachment(),
                new Attachment
                {
                    ContentType = "application/octet-stream",
                    Name = "upload.png",
                    Content = new { downloadUrl = "https://files.contoso.com/upload.png" }
                },
                ImageAttachment()
            ]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { "Bearer connector-token", null, "Bearer connector-token" }, handler.AuthorizationHeaders);
            Assert.Equal("upload.png", result[1].Filename);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RegisteredDownloader_InlineImageRedirectDowngrade_IsNotRequested(bool validationEnabled)
        {
            var handler = new RecordingHandler((_, count) => count == 1
                ? Redirect("http://files.contoso.com/image.png")
                : ImageResponse(FromHex(PngBytes)));
            var scenario = CreateScenario(handler, [ImageAttachment()], validationEnabled);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Empty(result);
            Assert.Equal(new[] { ImageUrl }, handler.RequestUris);
        }

        [Fact]
        public async Task RegisteredDownloader_InlineImageRedirect_KeepsBearerOnlyUntilLeavingOrigin()
        {
            var handler = new RecordingHandler((_, count) => count switch
            {
                1 => Redirect("/amer/redirected.png"),
                2 => Redirect("https://cdn.contoso.com/image.png"),
                3 => Redirect("https://files.contoso.com/amer/returned.png"),
                _ => ImageResponse(FromHex(PngBytes))
            });
            var scenario = CreateScenario(handler, [ImageAttachment()]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Single(result);
            Assert.Equal(new[] { "Bearer connector-token", "Bearer connector-token", null, null }, handler.AuthorizationHeaders);
        }

        [Fact]
        public async Task RegisteredDownloader_InlineImagePrivateRedirect_IsNotRequested()
        {
            var handler = new RecordingHandler((_, _) => Redirect("https://169.254.169.254/image.png"));
            var scenario = CreateScenario(handler, [ImageAttachment()]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Empty(result);
            Assert.Equal(new[] { ImageUrl }, handler.RequestUris);
        }

        [Fact]
        public async Task RegisteredDownloader_GifHintWithPngBytes_ReturnsPngAndDoesNotDownloadHtml()
        {
            var handler = new RecordingHandler((_, _) => ImageResponse(FromHex(PngBytes), "image/gif"));
            var scenario = CreateScenario(handler,
            [
                HtmlAttachment("gif"),
                ImageAttachment()
            ]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            var file = Assert.Single(result);
            Assert.Equal("image/png", file.ContentType);
            Assert.Equal("image-pic-1.png", file.Filename);
            Assert.Equal(new[] { ImageUrl }, handler.RequestUris);
        }

        [Theory]
        [InlineData("jpeg", "image/jpeg", "image-pic-1.jpg")]
        [InlineData("avif", "application/octet-stream", "image-pic-1")]
        public async Task RegisteredDownloader_UnknownBytes_UsesHtmlHintWithoutInventingFormat(
            string hint, string contentType, string filename)
        {
            var handler = new RecordingHandler((_, _) => ImageResponse([0x42, 0x4d], "image/bmp"));
            var scenario = CreateScenario(handler, [ImageAttachment(), HtmlAttachment(hint)]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            var file = Assert.Single(result);
            Assert.Equal(new byte[] { 0x42, 0x4d }, file.Content.ToArray());
            Assert.Equal(contentType, file.ContentType);
            Assert.Equal(filename, file.Filename);
        }

        [Theory]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.UnsupportedMediaType)]
        public async Task RegisteredDownloader_InlineImageHttpFailure_SkipsWithoutFallback(HttpStatusCode status)
        {
            var handler = new RecordingHandler((_, _) => new HttpResponseMessage(status));
            var scenario = CreateScenario(handler, [ImageAttachment()]);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Empty(result);
            Assert.Equal(new[] { ImageUrl }, handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RegisteredDownloader_AnonymousInlineImage_DoesNotResolveConnectorCredential()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler, [ImageAttachment()], useAnonymous: true);

            var result = await DownloadAsync(scenario.App, scenario.Context);

            Assert.Single(result);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            scenario.Transport.Verify(value => value.GetHttpClientAsync(), Times.Never);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CorePipeline_PopulatesInputFilesWithoutTeamsExtension()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler, [ImageAttachment()]);
            IList<InputFile> result = null;
            scenario.App.OnActivity(ActivityTypes.Message, (_, state, _) =>
            {
                result = state.Temp.InputFiles;
                return Task.CompletedTask;
            });

            await scenario.App.OnTurnAsync(scenario.Context, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(new[] { ImageUrl }, handler.RequestUris);
            scenario.Transport.Verify(value => value.GetHttpClientAsync(), Times.Once);
        }

        private static Attachment ImageAttachment(string contentUrl = ImageUrl) => new()
        {
            ContentType = "image/png",
            ContentUrl = contentUrl
        };

        private static Attachment HtmlAttachment(string format) => new()
        {
            ContentType = "text/html",
            Content = $"""<img itemtype="http://schema.skype.com/AMSImage" itemid="pic-1" itemscope="{format}">"""
        };

        private static (AgentApplication App, TurnContext Context, Mock<IConnections> Connections, Mock<IRestTransport> Transport)
            CreateScenario(RecordingHandler handler, IList<Attachment> attachments, bool validationEnabled = true, bool useAnonymous = false)
        {
            var connections = new Mock<IConnections>(MockBehavior.Strict);
            var connector = new Mock<IConnectorClient>();
            var transport = connector.As<IRestTransport>();
            transport.SetupGet(value => value.Endpoint).Returns(new Uri(ServiceUrl));
            transport.Setup(value => value.GetHttpClientAsync()).Returns(() =>
            {
                var client = new HttpClient(new RecordingHandler(), disposeHandler: false);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "connector-token");
                return Task.FromResult(client);
            });
            var context = new TurnContext(new SimpleAdapter(), new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ServiceUrl = ServiceUrl,
                From = new ChannelAccount("user"),
                Recipient = new ChannelAccount("agent"),
                Conversation = new ConversationAccount(id: "conversation"),
                Attachments = attachments
            }, new ClaimsIdentity());
            context.Services.Set(connector.Object);
            var app = new AgentApplication(new AgentApplicationOptions(() => new TurnState(new MemoryStorage()))
            {
                Connections = connections.Object,
                LoggerFactory = NullLoggerFactory.Instance,
                FileDownloaders =
                [
                    AttachmentDownloaderTests.CreateM365Downloader(handler, connections.Object, validationEnabled, useAnonymous)
                ]
            });
            return (app, context, connections, transport);
        }

        private static Task<IList<InputFile>> DownloadAsync(AgentApplication app, ITurnContext context) =>
            Assert.Single(app.Options.FileDownloaders).DownloadFilesAsync(context, null, CancellationToken.None);

        private static byte[] FromHex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static HttpResponseMessage ImageResponse(byte[] bytes, string contentType = "image/png")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return response;
        }

        private static HttpResponseMessage Redirect(string url) => new(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri(url, UriKind.RelativeOrAbsolute) }
        };

        private sealed class RecordingHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory = null) : HttpMessageHandler
        {
            public List<string> RequestUris { get; } = [];
            public List<string> AuthorizationHeaders { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUris.Add(request.RequestUri.AbsoluteUri);
                AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
                return Task.FromResult(responseFactory?.Invoke(request, RequestUris.Count)
                    ?? ImageResponse(FromHex(PngBytes)));
            }
        }
    }
}
