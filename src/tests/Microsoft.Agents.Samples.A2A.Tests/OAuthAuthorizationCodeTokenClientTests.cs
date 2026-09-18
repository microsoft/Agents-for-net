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

public class OAuthAuthorizationCodeTokenClientTests
{
    [Fact]
    public async Task AcquireTokenAsync_UsesAdvertisedAuthorizationCodeFlowWithoutProviderSpecificCode()
    {
        var receiver = new CapturingAuthorizationCodeReceiver("authorization-code");
        var handler = new CapturingTokenHandler(
            """{ "access_token": "linkedin-token", "token_type": "Bearer", "expires_in": 3600, "refresh_token": "refresh-token" }""");
        using var httpClient = new HttpClient(handler);
        var sut = new OAuthAuthorizationCodeTokenClient(httpClient, receiver);
        OAuthConnectionOptions connection = new()
        {
            ClientId = "linkedin-client-id",
            ClientSecret = "linkedin-client-secret",
            RedirectUri = new Uri("http://localhost:8400/callback/"),
            AllowedOrigins = [new Uri("https://www.linkedin.com")],
            TokenEndpointAuthenticationMethod = OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
            UsePkce = true,
        };

        OAuthAccessToken token = await sut.AcquireTokenAsync(
            CreateAuthorizationCodeAuthentication(),
            connection,
            CancellationToken.None);

        Assert.Equal("linkedin-token", token.AccessToken);
        Assert.Equal(TimeSpan.FromHours(1), token.ExpiresIn);
        Assert.Equal("refresh-token", token.RefreshToken);
        Assert.NotNull(receiver.AuthorizationUri);
        Assert.Equal("https", receiver.AuthorizationUri.Scheme);
        Assert.Equal("www.linkedin.com", receiver.AuthorizationUri.Host);
        Assert.Equal("/oauth/v2/authorization", receiver.AuthorizationUri.AbsolutePath);
        Assert.Contains("response_type=code", receiver.AuthorizationUri.Query, StringComparison.Ordinal);
        Assert.Contains("client_id=linkedin-client-id", receiver.AuthorizationUri.Query, StringComparison.Ordinal);
        Assert.Contains("scope=openid+profile", receiver.AuthorizationUri.Query, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", receiver.AuthorizationUri.Query, StringComparison.Ordinal);
        Assert.Contains("state=", receiver.AuthorizationUri.Query, StringComparison.Ordinal);
        Assert.Equal("https://www.linkedin.com/oauth/v2/accessToken", handler.RequestUri?.AbsoluteUri);
        Assert.Contains("grant_type=authorization_code", handler.Content, StringComparison.Ordinal);
        Assert.Contains("code=authorization-code", handler.Content, StringComparison.Ordinal);
        Assert.Contains("client_id=linkedin-client-id", handler.Content, StringComparison.Ordinal);
        Assert.Contains("client_secret=linkedin-client-secret", handler.Content, StringComparison.Ordinal);
        Assert.Contains("code_verifier=", handler.Content, StringComparison.Ordinal);
    }

    private static A2AAgentCardAuthentication CreateAuthorizationCodeAuthentication()
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["linkedin"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            AuthorizationCode = new()
                            {
                                AuthorizationUrl = "https://www.linkedin.com/oauth/v2/authorization",
                                TokenUrl = "https://www.linkedin.com/oauth/v2/accessToken",
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
                        ["linkedin"] = new() { List = ["openid", "profile"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }

    private sealed class CapturingAuthorizationCodeReceiver(string authorizationCode)
        : IOAuthAuthorizationCodeReceiver
    {
        public Uri? AuthorizationUri { get; private set; }

        public Task<string> ReceiveCodeAsync(
            Uri authorizationUri,
            Uri redirectUri,
            string expectedState,
            CancellationToken cancellationToken)
        {
            AuthorizationUri = authorizationUri;
            return Task.FromResult(authorizationCode);
        }
    }

    private sealed class CapturingTokenHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string Content { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
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
