// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        OAuthAccessToken token = await sut.AcquireTokenAsync(
            CreateBinding(),
            CancellationToken.None);

        Assert.Equal("application-token", token.AccessToken);
        Assert.Equal(TimeSpan.FromHours(1), token.ExpiresIn);
        Assert.Equal("https://identity.example.com/oauth/token", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Basic", handler.AuthorizationScheme);
        Assert.Contains("grant_type=client_credentials", handler.Content, StringComparison.Ordinal);
        Assert.Contains("scope=agent.read+agent.write", handler.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcquireTokenAsync_PlaceholderClientId_ThrowsBeforeSendingCredentials()
    {
        var handler = new CapturingTokenHandler("{}");
        using var httpClient = new HttpClient(handler);
        var sut = new OAuthClientCredentialsTokenClient(httpClient);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(clientId: "00000000-0000-0000-0000-000000000000"),
                CancellationToken.None));

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(handler.RequestUri);
        Assert.DoesNotContain("application-client-secret", exception.Message, StringComparison.Ordinal);
    }

    private static OAuthCredentialBinding CreateBinding(string clientId = "application-client-id")
        => new(
            ProviderId: "application-provider",
            ProviderType: OAuthCredentialProviderType.GenericOAuth2,
            RegistrationId: "application",
            Registration: new OAuthClientRegistration(
                "application",
                [A2AOAuthFlowType.ClientCredentials],
                clientId,
                "application-client-secret",
                RedirectUri: null,
                OAuthTokenEndpointAuthenticationMethod.ClientSecretBasic,
                UsePkce: false),
            FlowType: A2AOAuthFlowType.ClientCredentials,
            AuthorizationEndpoint: null,
            DeviceAuthorizationEndpoint: null,
            TokenEndpoint: new Uri("https://identity.example.com/oauth/token"),
            MetadataUrl: null,
            RegistrationEndpoint: null,
            EffectiveScopes: ["agent.read", "agent.write"],
            ProviderIdentity: "application-provider");

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
