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

        OAuthAccessToken token = await sut.AcquireTokenAsync(
            CreateBinding(),
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

    [Fact]
    public async Task AcquireTokenAsync_PkceProviderBindingWithoutPkce_ThrowsBeforeOpeningBrowser()
    {
        bool receiverInvoked = false;
        var receiver = new CapturingAuthorizationCodeReceiver("authorization-code")
        {
            OnReceive = () => receiverInvoked = true,
        };
        var handler = new CapturingTokenHandler("{}");
        using var httpClient = new HttpClient(handler);
        var sut = new OAuthAuthorizationCodeTokenClient(httpClient, receiver);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(
                    providerType: OAuthCredentialProviderType.GenericOAuth2Pkce,
                    usePkce: false),
                CancellationToken.None));

        Assert.Contains("PKCE", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(receiverInvoked);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task AcquireTokenAsync_PlaceholderClientId_ThrowsBeforeOpeningBrowser()
    {
        bool receiverInvoked = false;
        var receiver = new CapturingAuthorizationCodeReceiver("authorization-code")
        {
            OnReceive = () => receiverInvoked = true,
        };
        var handler = new CapturingTokenHandler("{}");
        using var httpClient = new HttpClient(handler);
        var sut = new OAuthAuthorizationCodeTokenClient(httpClient, receiver);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(clientId: "00000000-0000-0000-0000-000000000000"),
                CancellationToken.None));

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(receiverInvoked);
        Assert.Null(handler.RequestUri);
    }

    private static OAuthCredentialBinding CreateBinding(
        OAuthCredentialProviderType providerType = OAuthCredentialProviderType.GenericOAuth2Pkce,
        bool usePkce = true,
        string clientId = "linkedin-client-id")
        => new(
            ProviderId: "linkedin-provider",
            ProviderType: providerType,
            RegistrationId: "browser",
            Registration: new OAuthClientRegistration(
                "browser",
                [A2AOAuthFlowType.AuthorizationCode],
                clientId,
                "linkedin-client-secret",
                new Uri("http://localhost:8400/callback/"),
                OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
                UsePkce: usePkce),
            FlowType: A2AOAuthFlowType.AuthorizationCode,
            AuthorizationEndpoint: new Uri("https://www.linkedin.com/oauth/v2/authorization"),
            DeviceAuthorizationEndpoint: null,
            TokenEndpoint: new Uri("https://www.linkedin.com/oauth/v2/accessToken"),
            MetadataUrl: null,
            RegistrationEndpoint: null,
            EffectiveScopes: ["openid", "profile"],
            ProviderIdentity: "linkedin-provider");

    private sealed class CapturingAuthorizationCodeReceiver(string authorizationCode)
        : IOAuthAuthorizationCodeReceiver
    {
        public Uri? AuthorizationUri { get; private set; }

        public Action? OnReceive { get; init; }

        public Task<string> ReceiveCodeAsync(
            Uri authorizationUri,
            Uri redirectUri,
            string expectedState,
            CancellationToken cancellationToken)
        {
            OnReceive?.Invoke();
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
