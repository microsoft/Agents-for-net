// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class OAuthDeviceCodeTokenClientTests
{
    [Fact]
    public async Task AcquireTokenAsync_UsesAdvertisedEndpointsForAnyTrustedProvider()
    {
        var handler = new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://identity.example.com/device", "expires_in": 900, "interval": 5 }""",
            """{ "access_token": "provider-token", "token_type": "bearer", "scope": "profile.read", "expires_in": 3600, "refresh_token": "refresh-token" }""");
        using var client = new HttpClient(handler);
        var output = new StringWriter();
        var sut = new OAuthDeviceCodeTokenClient(
            client,
            output,
            static (_, _) => Task.CompletedTask);

        OAuthAccessToken token = await sut.AcquireTokenAsync(
            CreateBinding(),
            CancellationToken.None);

        Assert.Equal("provider-token", token.AccessToken);
        Assert.Equal(TimeSpan.FromHours(1), token.ExpiresIn);
        Assert.Equal("refresh-token", token.RefreshToken);
        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal("https://identity.example.com/oauth/device", request.RequestUri?.AbsoluteUri);
                Assert.Contains("scope=profile.read+offline_access", request.Content, StringComparison.Ordinal);
            },
            request => Assert.Equal("https://identity.example.com/oauth/token", request.RequestUri?.AbsoluteUri));
        Assert.Contains("ABCD-EFGH", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("provider-token", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcquireTokenAsync_AuthorizationPendingAndSlowDown_AdjustPolling()
    {
        var delays = new List<TimeSpan>();
        var handler = new SequenceResponseHandler(
            (HttpStatusCode.OK, """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://identity.example.com/device", "expires_in": 900, "interval": 5 }"""),
            (HttpStatusCode.BadRequest, """{ "error": "authorization_pending" }"""),
            (HttpStatusCode.BadRequest, """{ "error": "slow_down" }"""),
            (HttpStatusCode.OK, """{ "access_token": "provider-token" }"""));
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(
            client,
            TextWriter.Null,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        OAuthAccessToken token = await sut.AcquireTokenAsync(
            CreateBinding(),
            CancellationToken.None);

        Assert.Equal("provider-token", token.AccessToken);
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)],
            delays);
    }

    [Fact]
    public async Task AcquireTokenAsync_MissingClientId_ThrowsBeforeSendingRequest()
    {
        var handler = new SequenceJsonHandler("{}");
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(client, TextWriter.Null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(clientId: string.Empty),
                CancellationToken.None));

        Assert.Contains("ClientId", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AcquireTokenAsync_PlaceholderClientId_ThrowsBeforeSendingRequest()
    {
        var handler = new SequenceJsonHandler("{}");
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(client, TextWriter.Null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(clientId: "00000000-0000-0000-0000-000000000000"),
                CancellationToken.None));

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AcquireTokenAsync_DeviceAuthorizationError_IncludesProviderDetails()
    {
        var handler = new SequenceResponseHandler(
            (HttpStatusCode.BadRequest, """
                {
                  "error": "invalid_request",
                  "error_description": "AADSTS50059: No tenant-identifying information found."
                }
                """));
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(client, TextWriter.Null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(),
                CancellationToken.None));

        Assert.Contains("invalid_request", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AADSTS50059", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("expired_token", "expired")]
    [InlineData("access_denied", "denied")]
    public async Task AcquireTokenAsync_TerminalErrors_AreReported(string error, string expected)
    {
        var handler = new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://identity.example.com/device", "expires_in": 900, "interval": 5 }""",
            $@"{{ ""error"": ""{error}"", ""error_description"": ""provider details"" }}");
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(
            client,
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateBinding(),
                CancellationToken.None));

        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider details", exception.Message, StringComparison.Ordinal);
    }

    private static OAuthCredentialBinding CreateBinding(string clientId = "provider-client-id")
        => new(
            ProviderId: "provider",
            ProviderType: OAuthCredentialProviderType.GenericOAuth2,
            RegistrationId: "device-code",
            Registration: new OAuthClientRegistration(
                "device-code",
                [A2AOAuthFlowType.DeviceCode],
                clientId,
                ClientSecret: null,
                RedirectUri: null,
                TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                UsePkce: true),
            FlowType: A2AOAuthFlowType.DeviceCode,
            AuthorizationEndpoint: null,
            DeviceAuthorizationEndpoint: new Uri("https://identity.example.com/oauth/device"),
            TokenEndpoint: new Uri("https://identity.example.com/oauth/token"),
            MetadataUrl: null,
            RegistrationEndpoint: null,
            EffectiveScopes: ["profile.read", "offline_access"],
            ProviderIdentity: "provider");

    private sealed class SequenceJsonHandler(params string[] responses) : HttpMessageHandler
    {
        private int _index;

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.RequestUri,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            string response = responses[Math.Min(_index++, responses.Length - 1)];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record CapturedRequest(Uri? RequestUri, string Content);

    private sealed class SequenceResponseHandler(params (HttpStatusCode StatusCode, string Content)[] responses)
        : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            (HttpStatusCode statusCode, string content) = responses[Math.Min(_index++, responses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
