// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System.Linq;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class AgentEndpointExtensionsTests
    {
        [Fact]
        public void MapTeamsAgenticOAuthCallbackEndpoint_ShouldMapDefaultAnonymousGetEndpoint()
        {
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            ((IEndpointRouteBuilder)app).MapTeamsAgenticOAuthCallbackEndpoint();

            var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>());

            Assert.Equal("/auth/callback", endpoint.RoutePattern.RawText);
            Assert.Contains("GET", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        }

        [Fact]
        public void MapTeamsAgenticOAuthCallbackEndpoint_ShouldUseProvidedPath()
        {
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            ((IEndpointRouteBuilder)app).MapTeamsAgenticOAuthCallbackEndpoint("/custom/callback");

            var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>());

            Assert.Equal("/custom/callback", endpoint.RoutePattern.RawText);
        }
    }
}
