// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class OAuthClientCredentialsTokenClientTests
{
    [Fact]
    public async Task AcquireTokenAsync_UsesAdvertisedClientCredentialsFlow()
    {
        var handler = new CapturingTokenHandler(
            """{ "access_token": "application-token", "token_type": "Bearer", "expires_in": 3600 }""");
        using var httpClient = new HttpClient(handler);
        var sut = new OAuthClientCredentialsTokenClient(httpClient);
        OAuthConnectionOptions connection = new()
        {
            ClientId = "application-client-id",
            ClientSecret = "application-client-secret",
            AllowedOrigins = [new Uri("https://identity.example.com")],
            TokenEndpointAuthenticationMethod = OAuthTokenEndpointAuthenticationMethod.ClientSecretBasic,
        };

        OAuthAccessToken token = await sut.AcquireTokenAsync(
            CreateClientCredentialsAuthentication(),
            connection,
            CancellationToken.None);

        Assert.Equal("application-token", token.AccessToken);
        Assert.Equal(TimeSpan.FromHours(1), token.ExpiresIn);
        Assert.Equal("https://identity.example.com/oauth/token", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Basic", handler.AuthorizationScheme);
        Assert.Contains("grant_type=client_credentials", handler.Content, StringComparison.Ordinal);
        Assert.Contains("scope=agent.read+agent.write", handler.Content, StringComparison.Ordinal);
    }

    private static A2AAgentCardAuthentication CreateClientCredentialsAuthentication()
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["application"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            ClientCredentials = new()
                            {
                                TokenUrl = "https://identity.example.com/oauth/token",
                            },
                        },
                    },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["application"] = new() { List = ["agent.read", "agent.write"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.App);
    }

    private sealed class CapturingTokenHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string Content { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            Content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
