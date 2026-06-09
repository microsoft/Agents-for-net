// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.UserAuth.TeamsAgentic;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class TeamsAgenticOAuthCallbackEndpointTests
    {
        [Fact]
        public async Task HandleCallbackAsync_ShouldReturnBadRequest_WhenCodeOrStateIsMissing()
        {
            var httpContext = CreateHttpContext(new Dictionary<string, string>());

            await TeamsAgenticOAuthCallbackEndpoint.HandleCallbackAsync(httpContext);

            Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
            Assert.Contains("Missing authorization code or state parameter.", await ReadResponseBodyAsync(httpContext));
        }

        [Fact]
        public async Task HandleCallbackAsync_ShouldReturnBadRequest_WhenStateIsNotFound()
        {
            var httpContext = CreateHttpContext(new Dictionary<string, string>
            {
                ["code"] = "auth-code",
                ["state"] = "missing-state"
            });

            await TeamsAgenticOAuthCallbackEndpoint.HandleCallbackAsync(httpContext);

            Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
            Assert.Contains("Invalid or expired state. Please try signing in again.", await ReadResponseBodyAsync(httpContext));
        }

        private static DefaultHttpContext CreateHttpContext(Dictionary<string, string> queryParams)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton(Mock.Of<IConnections>());
            services.AddSingleton(Mock.Of<Builder.IChannelAdapter>());
            services.AddSingleton(Mock.Of<Builder.IAgent>());
            services.AddTransient<TeamsAgenticCallbackHandler>();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            };

            httpContext.Response.Body = new MemoryStream();
            httpContext.Request.QueryString = QueryString.Create(queryParams);

            return httpContext;
        }

        private static async Task<string> ReadResponseBodyAsync(DefaultHttpContext httpContext)
        {
            httpContext.Response.Body.Position = 0;
            using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
    }
}
