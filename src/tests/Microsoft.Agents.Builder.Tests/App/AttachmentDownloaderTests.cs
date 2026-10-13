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
                Channels.Msteams,
                new { downloadUrl = "https://files.contoso.com/doc.txt" });
            context.Identity = new System.Security.Claims.ClaimsIdentity();

            var result = await downloader.DownloadFilesAsync(context, null, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(new[] { "Bearer test-token", null }, handler.AuthorizationHeaders);
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
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
            return factory.Object;
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
