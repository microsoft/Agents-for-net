// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = CreateValidatedDownloader(handler, validator, PublicAddressResolver());
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

        [Fact]
        public async Task DownloadFilesAsync_UserInfoInAuthority_SkipsAttachment_NoOutboundRequest()
        {
            var handler = new RecordingHandler();
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = new AttachmentDownloader(factory, validator);
            var context = CreateContext("https://user:password@files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_PrivateIpAddress_SkipsAttachment_NoOutboundRequest()
        {
            var handler = new RecordingHandler();
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "10.0.0.1" }
            });

            var downloader = new AttachmentDownloader(factory, validator);
            var context = CreateContext("https://10.0.0.1/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Theory]
        [InlineData("https://127.0.0.1/doc.txt")]
        [InlineData("https://172.16.0.1/doc.txt")]
        [InlineData("https://192.168.0.1/doc.txt")]
        [InlineData("https://169.254.10.20/doc.txt")]
        [InlineData("https://[::1]/doc.txt")]
        [InlineData("https://[fe80::1]/doc.txt")]
        [InlineData("https://[fc00::1]/doc.txt")]
        [InlineData("https://[fd12:3456:789a::1]/doc.txt")]
        [InlineData("https://[::ffff:192.168.0.1]/doc.txt")]
        public async Task DownloadFilesAsync_BlockedIpRange_SkipsAttachment_NoOutboundRequest(string contentUrl)
        {
            var handler = new RecordingHandler();
            var validator = new Mock<IOutboundHostValidator>();
            validator.SetupGet(v => v.Enabled).Returns(true);
            validator.Setup(v => v.IsAllowed(It.IsAny<Uri>())).Returns(true);

            var downloader = CreateValidatedDownloader(handler, validator.Object, PublicAddressResolver());
            var context = CreateContext(contentUrl);

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_HostnameResolvesToPrivateAddress_SkipsAttachment_NoOutboundRequest()
        {
            var handler = new RecordingHandler();
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });
            var resolver = new StubHostAddressResolver(IPAddress.Parse("192.168.10.20"));

            var downloader = CreateValidatedDownloader(handler, validator, resolver);
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_PrivateAddressAllowedWhenValidatorExplicitlyConfigured()
        {
            var handler = new RecordingHandler();
            var resolver = new StubHostAddressResolver(IPAddress.Parse("10.0.0.10"));
            var validator = new OutboundHostValidator(
                new OutboundHostValidatorOptions
                {
                    Enabled = true,
                    AllowPrivateNetworkAddresses = true,
                    Hosts = new List<string> { "files.contoso.com" }
                },
                resolver);
            var downloader = CreateValidatedDownloader(handler, validator, resolver);
            var context = CreateContext("https://files.contoso.com/document.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_HostnameResolvesToPublicAndPrivateAddresses_SkipsAttachment_NoOutboundRequest()
        {
            var handler = new RecordingHandler();
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });
            var resolver = new StubHostAddressResolver(
                IPAddress.Parse("203.0.113.10"),
                IPAddress.Parse("10.0.0.5"));

            var downloader = CreateValidatedDownloader(handler, validator, resolver);
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_AllowedRedirect_DownloadsAttachment()
        {
            var handler = new RecordingHandler((request, callCount) =>
            {
                if (callCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers = { Location = new Uri("https://cdn.contoso.com/doc.txt") }
                    };
                }

                return CreateFileResponse();
            });
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = CreateValidatedDownloader(handler, validator, PublicAddressResolver());
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_RelativeRedirect_DownloadsAttachment()
        {
            var handler = new RecordingHandler((request, callCount) =>
            {
                if (callCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers = { Location = new Uri("/redirected/doc.txt", UriKind.Relative) }
                    };
                }

                return CreateFileResponse();
            });
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = CreateValidatedDownloader(handler, validator, PublicAddressResolver());
            var context = CreateContext("https://files.contoso.com/original/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(
                new[] { "https://files.contoso.com/original/doc.txt", "https://files.contoso.com/redirected/doc.txt" },
                handler.RequestUris);
        }

        [Fact]
        public async Task DownloadFilesAsync_DisallowedRedirect_SkipsAttachment_NoRequestToRedirectTarget()
        {
            var handler = new RecordingHandler((request, callCount) =>
                new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://evil.example.com/doc.txt") }
                });
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = CreateValidatedDownloader(handler, validator, PublicAddressResolver());
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_FiftyRedirects_DownloadsAttachment()
        {
            var handler = new RecordingHandler((request, callCount) =>
            {
                if (callCount <= 50)
                {
                    return new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers = { Location = new Uri($"/redirect-{callCount}", UriKind.Relative) }
                    };
                }

                return CreateFileResponse();
            });
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            var downloader = CreateValidatedDownloader(handler, validator, PublicAddressResolver());
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(51, handler.CallCount);
        }

        [Fact]
        public async Task M365DownloadFilesAsync_AllowedRedirect_DownloadsAttachment()
        {
            var handler = new RecordingHandler((request, callCount) =>
            {
                if (callCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers = { Location = new Uri("https://cdn.contoso.com/doc.txt") }
                    };
                }

                return CreateFileResponse();
            });
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>();
            var downloader = new M365AttachmentDownloader(
                connections.Object,
                factory,
                new M365AttachmentDownloaderOptions { UseAnonymous = true },
                validator,
                PublicAddressResolver(),
                new HttpClient(handler));
            var context = CreateContext(
                "https://files.contoso.com/doc.txt",
                Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/doc.txt" });

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task M365DownloadFilesAsync_AllowedRedirect_AuthorizesOnlyInitialRequest()
        {
            var handler = new RecordingHandler((request, callCount) =>
            {
                if (callCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers = { Location = new Uri("https://cdn.contoso.com/doc.txt") }
                    };
                }

                return CreateFileResponse();
            });
            var factory = CreateFactory(handler);
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });
            var accessTokenProvider = new Mock<Microsoft.Agents.Authentication.IAccessTokenProvider>();
            accessTokenProvider
                .Setup(provider => provider.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), false))
                .ReturnsAsync("test-token");
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>();
            connections
                .Setup(value => value.GetTokenProvider(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>()))
                .Returns(accessTokenProvider.Object);
            var downloader = new M365AttachmentDownloader(
                connections.Object,
                factory,
                new M365AttachmentDownloaderOptions(),
                validator,
                PublicAddressResolver(),
                new HttpClient(handler));
            var context = CreateContext(
                "https://files.contoso.com/doc.txt",
                Channels.Msteams);
            context.Identity = new System.Security.Claims.ClaimsIdentity();

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(new[] { "Bearer test-token", null }, handler.AuthorizationHeaders);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task M365DownloadFilesAsync_PreAuthorizedUrl_DoesNotResolveCredentialsOrSendBearer(
            bool validationEnabled, bool hasContentUrl)
        {
            var handler = new RecordingHandler();
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled);
            var contentUrl = hasContentUrl ? "https://files.contoso.com/view/doc.txt" : null;
            var context = CreateContext(
                contentUrl, Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/doc.txt?tempauth=signed" });
            context.Activity.Attachments[0].Name = "doc.txt";

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            var file = Assert.Single(result);
            Assert.Equal("file-bytes", file.Content.ToString());
            Assert.Equal("text/plain", file.ContentType);
            Assert.Equal(contentUrl, file.ContentUrl);
            Assert.Equal("doc.txt", file.Filename);
            Assert.Equal(new[] { "https://files.contoso.com/doc.txt?tempauth=signed" }, handler.RequestUris);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task M365DownloadFilesAsync_PreAuthorizedRedirect_DoesNotSendBearerOnAnyHop()
        {
            var handler = new RecordingHandler((_, count) => count == 1
                ? new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://cdn.contoso.com/doc.txt") }
                }
                : CreateFileResponse());
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled: true);
            var context = CreateContext(
                "https://files.contoso.com/view/doc.txt", Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/doc.txt?tempauth=signed" });

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(2, handler.CallCount);
            Assert.All(handler.AuthorizationHeaders, value => Assert.Null(value));
            connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task M365DownloadFilesAsync_MixedRoutes_UsesBearerOnlyForAuthenticatedContentUrls(
            bool validationEnabled)
        {
            var handler = new RecordingHandler();
            var accessTokenProvider = new Mock<Microsoft.Agents.Authentication.IAccessTokenProvider>();
            accessTokenProvider
                .Setup(value => value.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), false))
                .ReturnsAsync("test-token");
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>();
            connections.Setup(value => value.GetTokenProvider(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(), It.IsAny<IActivity>()))
                .Returns(accessTokenProvider.Object);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled);
            var context = CreateContext(null, Channels.Msteams);
            context.Identity = new System.Security.Claims.ClaimsIdentity();
            context.Activity.Attachments =
            [
                new Attachment
                {
                    ContentType = "application/octet-stream",
                    Content = new { downloadUrl = "https://files.contoso.com/signed-first.txt" }
                },
                new Attachment
                {
                    ContentType = "application/octet-stream",
                    ContentUrl = "https://files.contoso.com/authenticated-first.txt"
                },
                new Attachment
                {
                    ContentType = "application/octet-stream",
                    ContentUrl = "https://files.contoso.com/view/signed-last.txt",
                    Content = new { downloadUrl = "https://files.contoso.com/signed-last.txt" }
                },
                new Attachment
                {
                    ContentType = "application/octet-stream",
                    ContentUrl = "https://files.contoso.com/authenticated-last.txt"
                }
            ];

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Equal(4, result.Count);
            Assert.Equal(new[] { null, "Bearer test-token", null, "Bearer test-token" }, handler.AuthorizationHeaders);
            accessTokenProvider.Verify(value => value.GetAccessTokenAsync(
                It.IsAny<string>(), It.IsAny<IList<string>>(), false), Times.Once);
        }

        [Theory]
        [InlineData(true, HttpStatusCode.Unauthorized)]
        [InlineData(true, HttpStatusCode.Forbidden)]
        [InlineData(false, HttpStatusCode.Unauthorized)]
        [InlineData(false, HttpStatusCode.Forbidden)]
        public async Task M365DownloadFilesAsync_PreAuthorizedFailure_DoesNotFallbackToAuthenticatedUrl(
            bool validationEnabled, HttpStatusCode status)
        {
            var handler = new RecordingHandler((_, _) => new HttpResponseMessage(status));
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled);
            var context = CreateContext(
                "https://files.contoso.com/authenticated.txt", Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/expired.txt" });

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(new[] { "https://files.contoso.com/expired.txt" }, handler.RequestUris);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true, """{"downloadUrl":null}""")]
        [InlineData(false, """{"downloadUrl":null}""")]
        [InlineData(true, """{"downloadUrl":""}""")]
        [InlineData(false, """{"downloadUrl":""}""")]
        [InlineData(true, """{"downloadUrl":"not a URL"}""")]
        [InlineData(false, """{"downloadUrl":"not a URL"}""")]
        [InlineData(true, """{"downloadUrl":"http://files.contoso.com/doc.txt"}""")]
        [InlineData(false, """{"downloadUrl":"http://files.contoso.com/doc.txt"}""")]
        public async Task M365DownloadFilesAsync_InvalidPreAuthorizedUrl_DoesNotFallbackOrResolveCredentials(
            bool validationEnabled, string content)
        {
            var handler = new RecordingHandler();
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled);
            var context = CreateContext(
                "https://files.contoso.com/authenticated.txt", Channels.Msteams,
                JsonSerializer.Deserialize<JsonElement>(content));

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
            connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task M365DownloadFilesAsync_AnonymousContentUrl_DoesNotSendBearer(bool validationEnabled)
        {
            var handler = new RecordingHandler();
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(
                handler, connections.Object, validationEnabled, useAnonymous: true);
            var context = CreateContext("https://files.contoso.com/doc.txt", Channels.Msteams);

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task M365DownloadFilesAsync_PreAuthorizedUrl_DoesNotInheritDefaultBearer()
        {
            var handler = new RecordingHandler();
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(value => value.CreateClient(It.IsAny<string>())).Returns(() =>
            {
                var client = new HttpClient(handler, disposeHandler: false);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "configured-token");
                return client;
            });
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = new M365AttachmentDownloader(connections.Object, factory.Object);
            var context = CreateContext(
                "https://files.contoso.com/view/doc.txt", Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/doc.txt" });

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            connections.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task M365DownloadFilesAsync_PreAuthorizedJsonContent_DownloadsWithoutContentUrl()
        {
            var handler = new RecordingHandler();
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled: true);
            var context = CreateContext(
                null, Channels.M365Copilot,
                """{"downloadUrl":"https://files.contoso.com/doc.txt"}""");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Equal("file-bytes", Assert.Single(result).Content.ToString());
            Assert.Equal(new[] { "https://files.contoso.com/doc.txt" }, handler.RequestUris);
            Assert.Null(Assert.Single(handler.AuthorizationHeaders));
            connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("embedded content")]
        [InlineData("{not JSON")]
        public async Task M365DownloadFilesAsync_EmbeddedContent_DoesNotResolveCredentials(string content)
        {
            var handler = new RecordingHandler();
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>(MockBehavior.Strict);
            var downloader = CreateM365Downloader(handler, connections.Object, validationEnabled: true);
            var context = CreateContext(null, Channels.Msteams, content);

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Equal(content, Assert.Single(result).Content.ToObjectFromJson<string>());
            Assert.Equal(0, handler.CallCount);
            connections.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("image/jpeg", "image/jpeg", false)]
        [InlineData("image/webp", "image/webp", false)]
        [InlineData(null, "application/octet-stream", false)]
        [InlineData("image/jpeg", "image/jpeg", true)]
        [InlineData(null, "application/octet-stream", true)]
        public async Task M365DownloadFilesAsync_ResponseContentType_IsPreservedOrFallsBack(
            string responseContentType, string expectedContentType, bool signedUpload)
        {
            var handler = new RecordingHandler((_, _) =>
            {
                var response = CreateFileResponse();
                response.Content.Headers.ContentType = responseContentType == null
                    ? null
                    : new System.Net.Http.Headers.MediaTypeHeaderValue(responseContentType);
                return response;
            });
            var downloader = CreateM365Downloader(
                handler, new Mock<Microsoft.Agents.Authentication.IConnections>().Object,
                validationEnabled: true, useAnonymous: true);
            var context = CreateContext(
                signedUpload ? null : "https://files.contoso.com/doc.txt",
                Channels.Msteams,
                signedUpload ? new { downloadUrl = "https://files.contoso.com/doc.txt" } : null);
            if (signedUpload)
            {
                context.Activity.Attachments[0].ContentType = "application/vnd.microsoft.teams.file.download.info";
            }

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            var file = Assert.Single(result);
            Assert.Equal(expectedContentType, file.ContentType);
            Assert.Equal("file-bytes", file.Content.ToString());
        }

        [Fact]
        public async Task M365DownloadFilesAsync_PrivateRedirect_SkipsAttachment_NoRequestToRedirectTarget()
        {
            var handler = new RecordingHandler((request, callCount) =>
                new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://169.254.169.254/latest/meta-data") }
                });
            var factory = CreateFactory(handler);
            var validator = new Mock<IOutboundHostValidator>();
            validator.SetupGet(v => v.Enabled).Returns(true);
            validator.Setup(v => v.IsAllowed(It.IsAny<Uri>())).Returns(true);
            var connections = new Mock<Microsoft.Agents.Authentication.IConnections>();
            var downloader = new M365AttachmentDownloader(
                connections.Object,
                factory,
                new M365AttachmentDownloaderOptions { UseAnonymous = true },
                validator.Object,
                PublicAddressResolver(),
                new HttpClient(handler));
            var context = CreateContext(
                "https://files.contoso.com/doc.txt",
                Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/doc.txt" });

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task DownloadFilesAsync_AutoRedirectingFactoryCannotBypassValidation()
        {
            var factoryHandler = new AutoRedirectingHandler();
            var factory = CreateFactory(factoryHandler);
            var validatedHandler = new RecordingHandler((request, callCount) =>
                new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://169.254.169.254/latest/meta-data") }
                });
            var validator = new Mock<IOutboundHostValidator>();
            validator.SetupGet(v => v.Enabled).Returns(true);
            validator.Setup(v => v.IsAllowed(It.IsAny<Uri>())).Returns(true);
            var downloader = new AttachmentDownloader(
                factory,
                validator.Object,
                PublicAddressResolver(),
                new HttpClient(validatedHandler));
            var context = CreateContext("https://files.contoso.com/doc.txt");

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Empty(result);
            Assert.Equal(0, factoryHandler.CallCount);
            Assert.Equal(1, validatedHandler.CallCount);
        }

        private static TurnContext CreateContext(string contentUrl, string channelId = Channels.Webchat, object content = null)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = channelId,
                Attachments = new List<Attachment>
                {
                    new Attachment
                    {
                        ContentType = "application/octet-stream",
                        ContentUrl = contentUrl,
                        Content = content
                    }
                }
            };

            return new TurnContext(new SimpleAdapter(), activity);
        }

        private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
            return factory.Object;
        }

        internal static M365AttachmentDownloader CreateM365Downloader(
            HttpMessageHandler handler,
            Microsoft.Agents.Authentication.IConnections connections,
            bool validationEnabled,
            bool useAnonymous = false,
            M365AttachmentDownloaderOptions options = null,
            IOutboundHostValidator hostValidator = null)
        {
            return new M365AttachmentDownloader(
                connections,
                CreateFactory(handler),
                options ?? new M365AttachmentDownloaderOptions { UseAnonymous = useAnonymous },
                hostValidator ?? new OutboundHostValidator(new OutboundHostValidatorOptions
                {
                    Enabled = validationEnabled,
                    Hosts = ["contoso.com"]
                }),
                PublicAddressResolver(),
                new HttpClient(handler, disposeHandler: false));
        }

        private static AttachmentDownloader CreateValidatedDownloader(
            RecordingHandler handler,
            IOutboundHostValidator validator,
            IHostAddressResolver resolver)
        {
            return new AttachmentDownloader(
                CreateFactory(handler),
                validator,
                resolver,
                new HttpClient(handler));
        }

        private static IHostAddressResolver PublicAddressResolver()
        {
            return new StubHostAddressResolver(IPAddress.Parse("203.0.113.10"));
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responseFactory;
            private readonly List<string> _requestUris = new();
            private readonly List<string> _authorizationHeaders = new();

            public RecordingHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory = null)
            {
                _responseFactory = responseFactory;
            }

            public int CallCount { get; private set; }
            public IReadOnlyList<string> RequestUris => _requestUris;
            public IReadOnlyList<string> AuthorizationHeaders => _authorizationHeaders;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                _requestUris.Add(request.RequestUri.AbsoluteUri);
                _authorizationHeaders.Add(request.Headers.Authorization?.ToString());
                var response = _responseFactory?.Invoke(request, CallCount) ?? CreateFileResponse();
                return Task.FromResult(response);
            }
        }

        private sealed class AutoRedirectingHandler : HttpMessageHandler
        {
            public int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(CreateFileResponse());
            }
        }

        private static HttpResponseMessage CreateFileResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("file-bytes", Encoding.UTF8, "text/plain")
            };
        }

        private sealed class StubHostAddressResolver : IHostAddressResolver
        {
            private readonly IPAddress[] _addresses;

            public StubHostAddressResolver(params IPAddress[] addresses)
            {
                _addresses = addresses;
            }

            public Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken)
            {
                return Task.FromResult(_addresses);
            }
        }
    }
}
