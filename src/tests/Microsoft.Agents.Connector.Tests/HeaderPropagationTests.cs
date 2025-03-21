// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class HeaderPropagationTests
    {
        [Fact]
        public async Task Test_Propagate()
        {
            var httpAssert = new TestHttpMessageHandler
            {
                SendAssert = (httpRequest) =>
                {
                    Assert.True(httpRequest.Headers.Contains("header1"));
                    Assert.True(httpRequest.Headers.Contains("header2"));
                    Assert.Equal("header1Value", httpRequest.Headers.GetValues("header1").First());
                    Assert.Equal("header2Value", httpRequest.Headers.GetValues("header2").First());

                    var userAgent = httpRequest.Headers.GetValues("User-Agent").ToList();
                    Assert.NotNull(userAgent.Where(h => h.Equals("testProduct/1.0.0")).FirstOrDefault());

                    // Make sure our User-Agents values are there (at least one)
                    Assert.NotNull(userAgent.Where(h => h.StartsWith("Microsoft-Agent-SDK")).FirstOrDefault());
                }
            };

            var mockHttpFactory = new Mock<IHttpClientFactory>();
            mockHttpFactory
                .Setup(c => c.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(httpAssert));

            _ = new RequestContext(new HeaderPropagation());

            var connector = new RestConnectorClient(new Uri("https://somehost.com"), mockHttpFactory.Object, null);
            await connector.Conversations.SendToConversationAsync(new Activity() { Conversation = new ConversationAccount() { Id = "1"} });
        }
    }

    class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage HttpResponseMessage { get; set; }

        public Action<HttpRequestMessage> SendAssert { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendAssert?.Invoke(request);

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {  Content = new StringContent("{\"id\":\"2\"}") });
        }
    }

    class HeaderPropagation : IHeaderPropagation
    {
        public Dictionary<string, string> Headers => new Dictionary<string, string> { { "header1", "header1Value" }, { "header2", "header2Value" } };

        public string UserAgent => "testProduct/1.0.0";
    }
}
