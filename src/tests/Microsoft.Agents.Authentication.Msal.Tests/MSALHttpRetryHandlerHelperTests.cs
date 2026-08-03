// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MSALHttpRetryHandlerHelperTests
    {
        private readonly Mock<IOptions<MsalAuthConfigurationOptions>> _options;
        private readonly Mock<IServiceProvider> _service = new();
        private readonly MsalAuthConfigurationOptions _returnedOptions = new()
        {
            MSALRetryCount = 4
        };

        private readonly Mock<HttpMessageHandler> _handler = new();

        private const string RequestUri = "http://test.com";

        public MSALHttpRetryHandlerHelperTests()
        {
            _options = new Mock<IOptions<MsalAuthConfigurationOptions>>();
            _options.Setup(x => x.Value)
                .Returns(_returnedOptions)
                .Verifiable(Times.Once);
            
            _service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(_options.Object)
                .Verifiable(Times.Once);
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var retryHelper = new MSALHttpRetryHandlerHelper(_service.Object);

            Assert.NotNull(retryHelper);
            Mock.Verify(_service);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponse()
        {
            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
                .Verifiable(Times.Once);

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Mock.Verify(_handler);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponseAfterRetries()
        {
            var firstTimeoutContent = new TrackingHttpContent();
            var secondTimeoutContent = new TrackingHttpContent();
            var thirdTimeoutContent = new TrackingHttpContent();
            var successfulContent = new TrackingHttpContent();

            _handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = firstTimeoutContent })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = secondTimeoutContent })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = thirdTimeoutContent })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = successfulContent });

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            using var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(firstTimeoutContent.IsDisposed);
            Assert.True(secondTimeoutContent.IsDisposed);
            Assert.True(thirdTimeoutContent.IsDisposed);
            Assert.False(successfulContent.IsDisposed);
            _handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseOnNonRetryableFailure()
        {
            var badRequestContent = new TrackingHttpContent();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = badRequestContent })
                .Verifiable(Times.Once);

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(badRequestContent.IsDisposed);

            response.Dispose();
            Assert.True(badRequestContent.IsDisposed);
            Mock.Verify(_handler);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseAfterExhaustsAllRetries()
        {
            var firstTimeoutContent = new TrackingHttpContent();
            var secondTimeoutContent = new TrackingHttpContent();
            var thirdTimeoutContent = new TrackingHttpContent();
            var finalTimeoutContent = new TrackingHttpContent();

            _handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = firstTimeoutContent })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = secondTimeoutContent })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = thirdTimeoutContent })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout) { Content = finalTimeoutContent });

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
            Assert.True(firstTimeoutContent.IsDisposed);
            Assert.True(secondTimeoutContent.IsDisposed);
            Assert.True(thirdTimeoutContent.IsDisposed);
            Assert.False(finalTimeoutContent.IsDisposed);

            response.Dispose();
            Assert.True(finalTimeoutContent.IsDisposed);
            _handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        private sealed class TrackingHttpContent : HttpContent
        {
            public bool IsDisposed { get; private set; }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return Task.CompletedTask;
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return true;
            }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
