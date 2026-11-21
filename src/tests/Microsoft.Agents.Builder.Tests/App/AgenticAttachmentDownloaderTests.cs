// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AgenticAttachmentDownloaderTests
    {
        private const string FileDownloadInfo = "application/vnd.microsoft.teams.file.download.info";
        private const string SharingUrl = "https://contoso.sharepoint.com/report%20one.pdf?x=%2F";
        private const string SharingToken = "u!aHR0cHM6Ly9jb250b3NvLnNoYXJlcG9pbnQuY29tL3JlcG9ydCUyMG9uZS5wZGY_eD0lMkY";
        private const string GraphUrl = "https://graph.microsoft.com/v1.0/shares/" + SharingToken + "/driveItem/content";

        [Theory]
        [InlineData("personal", Channels.Msteams)]
        [InlineData("groupChat", Channels.Msteams)]
        [InlineData("channel", Channels.Msteams)]
        [InlineData(null, Channels.Msteams)]
        [InlineData("personal", Channels.M365Copilot)]
        public async Task DownloadFilesAsync_AgenticUser_ResolvesOriginalSharingUrlWithGraphScope(string scope, string channel)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Context.Activity.Conversation.ConversationType = scope;
            scenario.Context.Activity.ChannelId = channel;

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            var file = Assert.Single(files);
            Assert.Equal("file-bytes", file.Content.ToString());
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal("report.pdf", file.Filename);
            Assert.Equal(SharingUrl, file.ContentUrl);
            Assert.Equal(new[] { GraphUrl }, handler.RequestUris);
            Assert.Equal(new[] { "Bearer graph-user-token" }, handler.AuthorizationHeaders);
            scenario.Agentic.Verify(value => value.GetAgenticUserTokenAsync(
                "tenant", "instance", "agentic-user",
                It.Is<IList<string>>(scopes => scopes.SequenceEqual(new[] { "https://graph.microsoft.com/.default" })),
                It.IsAny<CancellationToken>()), Times.Once);
            scenario.Provider.Verify(value => value.GetAccessTokenAsync(
                It.IsAny<string>(), It.IsAny<IList<string>>(), false), Times.Never);
        }

        [Fact]
        public async Task DownloadFilesAsync_SovereignGraph_UsesMatchingEndpointAndScope()
        {
            var handler = new RecordingHandler();
            var options = ProtocolJsonSerializer.ToObject<M365AttachmentDownloaderOptions>(
                new { graphBaseUrl = "https://graph.microsoft.us/v1.0" });
            var scenario = CreateScenario(handler, options, hostValidator: new OutboundHostValidator(
                new OutboundHostValidatorOptions { Enabled = true, Hosts = ["graph.microsoft.us"] }));

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Single(files);
            Assert.Equal(new[] { "https://graph.microsoft.us/v1.0/shares/" + SharingToken + "/driveItem/content" }, handler.RequestUris);
            scenario.Agentic.Verify(value => value.GetAgenticUserTokenAsync(
                "tenant", "instance", "agentic-user",
                It.Is<IList<string>>(scopes => scopes.SequenceEqual(new[] { "https://graph.microsoft.us/.default" })),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DownloadFilesAsync_MixedRoutes_KeepGraphSignedAndBotCredentialsSeparate()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Provider.Setup(value => value.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), false))
                .ReturnsAsync("direct-token");
            var connector = new Mock<Microsoft.Agents.Connector.IConnectorClient>();
            connector.As<Microsoft.Agents.Connector.IRestTransport>().Setup(value => value.GetHttpClientAsync())
                .Returns(() =>
                {
                    var client = new HttpClient(new RecordingHandler(), disposeHandler: false);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "image-token");
                    return Task.FromResult(client);
                });
            scenario.Context.Services.Set(connector.Object);
            scenario.Context.Activity.Attachments =
            [
                SharedFile(),
                new Attachment
                {
                    ContentType = FileDownloadInfo, Name = "signed.pdf",
                    Content = new { downloadUrl = "https://files.contoso.com/signed.pdf" }
                },
                new Attachment
                {
                    ContentType = "image/png",
                    ContentUrl = "https://files.contoso.com/amer/v3/attachments/pic-1/views/original"
                },
                new Attachment
                {
                    ContentType = "application/octet-stream", ContentUrl = "https://files.contoso.com/direct.txt"
                },
                SharedFile()
            ];

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Equal(5, files.Count);
            Assert.Equal(
                new[] { "Bearer graph-user-token", null, "Bearer image-token", "Bearer direct-token", "Bearer graph-user-token" },
                handler.AuthorizationHeaders);
            scenario.Agentic.Verify(value => value.GetAgenticUserTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            scenario.Provider.Verify(value => value.GetAccessTokenAsync(
                It.IsAny<string>(), It.IsAny<IList<string>>(), false), Times.Once);
        }

        [Theory]
        [InlineData(RoleTypes.Agent, "personal")]
        [InlineData(RoleTypes.AgenticIdentity, "channel")]
        [InlineData(RoleTypes.AgenticUser, "unknown")]
        public async Task DownloadFilesAsync_UnsupportedActorOrScope_DoesNotResolveCredentials(string role, string scope)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Context.Activity.Recipient.Role = role;
            scenario.Context.Activity.Conversation.ConversationType = scope;

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null, "instance", "agentic-user")]
        [InlineData("tenant", null, "agentic-user")]
        [InlineData("tenant", "instance", null)]
        public async Task DownloadFilesAsync_IncompleteAgenticIdentity_DoesNotResolveCredentials(
            string tenant, string instance, string user)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Context.Activity.Recipient.TenantId = tenant;
            scenario.Context.Activity.Recipient.AgenticAppId = instance;
            scenario.Context.Activity.Recipient.AgenticUserId = user;

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("{bad JSON")]
        [InlineData("""{"downloadUrl":null}""")]
        public async Task DownloadFilesAsync_MalformedFileMetadata_IsNotReinterpretedAsGraph(string content)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Context.Activity.Attachments[0].Content = content;

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("http://graph.microsoft.com/v1.0")]
        [InlineData("https://user:password@graph.microsoft.com/v1.0")]
        [InlineData("https://graph.microsoft.com/v1.0?query=value")]
        [InlineData("not a URL")]
        public async Task DownloadFilesAsync_InvalidGraphEndpoint_IsRejectedBeforeCredentials(string endpoint)
        {
            var options = ProtocolJsonSerializer.ToObject<M365AttachmentDownloaderOptions>(new { graphBaseUrl = endpoint });
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler, options);

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task DownloadFilesAsync_AnonymousSharedFile_DoesNotUseAnotherIdentity()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler, new M365AttachmentDownloaderOptions { UseAnonymous = true });

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task DownloadFilesAsync_SignedUrlFailure_NeverFallsBackToGraph(HttpStatusCode status)
        {
            var handler = new RecordingHandler((_, _) => new HttpResponseMessage(status));
            var scenario = CreateScenario(handler);
            scenario.Context.Activity.Attachments[0].Content = new { downloadUrl = "https://files.contoso.com/expired.pdf" };

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Equal(new[] { "https://files.contoso.com/expired.pdf" }, handler.RequestUris);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DownloadFilesAsync_GraphRedirect_DropsBearerBeforeStorage(bool validationEnabled)
        {
            var handler = new RecordingHandler((_, count) => count == 1
                ? Redirect("https://files.contoso.com/download.pdf?tempauth=signed")
                : FileResponse());
            var scenario = CreateScenario(handler, validationEnabled: validationEnabled);

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Single(files);
            Assert.Equal(new[] { GraphUrl, "https://files.contoso.com/download.pdf?tempauth=signed" }, handler.RequestUris);
            Assert.Equal(new[] { "Bearer graph-user-token", null }, handler.AuthorizationHeaders);
        }

        [Fact]
        public async Task DownloadFilesAsync_GraphRedirectDowngrade_IsNotRequested()
        {
            var handler = new RecordingHandler((_, _) => Redirect("http://files.contoso.com/download.pdf"));
            var scenario = CreateScenario(handler, validationEnabled: false);

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Equal(new[] { GraphUrl }, handler.RequestUris);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task DownloadFilesAsync_GraphFailure_DoesNotFetchBrowsableUrlOrChangeIdentity(HttpStatusCode status)
        {
            var handler = new RecordingHandler((_, _) => new HttpResponseMessage(status));
            var scenario = CreateScenario(handler);

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Equal(new[] { GraphUrl }, handler.RequestUris);
            Assert.Equal(new[] { "Bearer graph-user-token" }, handler.AuthorizationHeaders);
            scenario.Provider.Verify(value => value.GetAccessTokenAsync(
                It.IsAny<string>(), It.IsAny<IList<string>>(), false), Times.Never);
        }

        [Fact]
        public async Task DownloadFilesAsync_TokenWithoutFilePermissions_IsSkippedBeforeGraphRequest()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Agentic.Setup(value => value.GetAgenticUserTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Jwt(new { scp = "User.Read Mail.Send" }));

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
        }

        [Fact]
        public async Task DownloadFilesAsync_TokenWithFilePermission_ProceedsToGraph()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Agentic.Setup(value => value.GetAgenticUserTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Jwt(new { scp = "User.Read Files.ReadWrite.All" }));

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Single(files);
            Assert.Equal(new[] { GraphUrl }, handler.RequestUris);
        }

        [Fact]
        public async Task DownloadFilesAsync_NonAgenticTokenProvider_IsNotUsedAsAppOnlyFallback()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            var appOnlyProvider = new Mock<IAccessTokenProvider>(MockBehavior.Strict);
            scenario.Connections.Setup(value => value.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>()))
                .Returns(appOnlyProvider.Object);

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            appOnlyProvider.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("http://contoso.sharepoint.com/report.pdf")]
        [InlineData("not a URL")]
        public async Task DownloadFilesAsync_InvalidSharingUrl_IsRejectedBeforeCredentials(string sharingUrl)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Context.Activity.Attachments[0].ContentUrl = sharingUrl;

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task DownloadFilesAsync_EmptyGraphToken_IsSkippedWithoutFallback(string token)
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Agentic.Setup(value => value.GetAgenticUserTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(token);

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Provider.Verify(value => value.GetAccessTokenAsync(
                It.IsAny<string>(), It.IsAny<IList<string>>(), false), Times.Never);
        }

        [Fact]
        public async Task DownloadFilesAsync_GraphTokenAcquisitionFailure_SkipsWithoutFallback()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Agentic.Setup(value => value.GetAgenticUserTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
                It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("credential unavailable"));

            var files = await DownloadAsync(scenario.Downloader, scenario.Context);

            Assert.Empty(files);
            Assert.Empty(handler.RequestUris);
            scenario.Provider.Verify(value => value.GetAccessTokenAsync(
                It.IsAny<string>(), It.IsAny<IList<string>>(), false), Times.Never);
        }

        [Fact]
        public async Task DownloadFilesAsync_GraphTokenCancellation_Propagates()
        {
            var handler = new RecordingHandler();
            var scenario = CreateScenario(handler);
            scenario.Agentic.Setup(value => value.GetAgenticUserTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
                It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DownloadAsync(scenario.Downloader, scenario.Context));

            Assert.Empty(handler.RequestUris);
        }

        private static Attachment SharedFile() => new()
        {
            ContentType = FileDownloadInfo,
            ContentUrl = SharingUrl,
            Name = "report.pdf",
            Content = new { uniqueId = "sharepoint-item-id", fileType = "pdf" }
        };

        private static (M365AttachmentDownloader Downloader, TurnContext Context, Mock<IConnections> Connections,
            Mock<IAccessTokenProvider> Provider, Mock<IAgenticTokenProvider> Agentic)
            CreateScenario(RecordingHandler handler, M365AttachmentDownloaderOptions options = null,
                bool validationEnabled = true, IOutboundHostValidator hostValidator = null)
        {
            var provider = new Mock<IAccessTokenProvider>(MockBehavior.Strict);
            var agentic = provider.As<IAgenticTokenProvider>();
            agentic.Setup(value => value.GetAgenticUserTokenAsync(
                "tenant", "instance", "agentic-user", It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("graph-user-token");
            var connections = new Mock<IConnections>(MockBehavior.Strict);
            connections.Setup(value => value.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>()))
                .Returns(provider.Object);
            var context = new TurnContext(new SimpleAdapter(), new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://files.contoso.com/amer/",
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.AgenticUser,
                    TenantId = "tenant",
                    AgenticAppId = "instance",
                    AgenticUserId = "agentic-user"
                },
                Conversation = new ConversationAccount { ConversationType = "personal" },
                Attachments = [SharedFile()]
            }, new ClaimsIdentity());
            return (AttachmentDownloaderTests.CreateM365Downloader(
                handler, connections.Object, validationEnabled, options: options, hostValidator: hostValidator),
                context, connections, provider, agentic);
        }

        private static Task<IList<InputFile>> DownloadAsync(M365AttachmentDownloader downloader, ITurnContext context) =>
            downloader.DownloadFilesAsync(context, null, CancellationToken.None);

        private static string Jwt(object claims) => "e30." +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)))
                .TrimEnd('=').Replace('/', '_').Replace('+', '-') + ".signature";

        private static HttpResponseMessage FileResponse() => new(HttpStatusCode.OK)
        {
            Content = new StringContent("file-bytes", Encoding.UTF8, "application/pdf")
        };

        private static HttpResponseMessage Redirect(string url) => new(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri(url) }
        };

        private sealed class RecordingHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory = null) : HttpMessageHandler
        {
            public List<string> RequestUris { get; } = [];
            public List<string> AuthorizationHeaders { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUris.Add(request.RequestUri.AbsoluteUri);
                AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
                return Task.FromResult(responseFactory?.Invoke(request, RequestUris.Count) ?? FileResponse());
            }
        }
    }
}
