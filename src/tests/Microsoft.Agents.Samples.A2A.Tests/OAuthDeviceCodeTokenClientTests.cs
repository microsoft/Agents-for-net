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
            CreateAuthentication(),
            CreateConnection(),
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
            CreateAuthentication(),
            CreateConnection(),
            CancellationToken.None);

        Assert.Equal("provider-token", token.AccessToken);
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)],
            delays);
    }

    [Fact]
    public async Task AcquireTokenAsync_UntrustedTokenOrigin_ThrowsBeforeSendingRequest()
    {
        var handler = new SequenceJsonHandler("{}");
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(client, TextWriter.Null);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            tokenUrl: "https://attacker.example/oauth/token");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(authentication, CreateConnection(), CancellationToken.None));

        Assert.Contains("not trusted", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AcquireTokenAsync_MissingClientId_ThrowsBeforeSendingRequest()
    {
        var handler = new SequenceJsonHandler("{}");
        using var client = new HttpClient(handler);
        var sut = new OAuthDeviceCodeTokenClient(client, TextWriter.Null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(
                CreateAuthentication(),
                new OAuthConnectionOptions
                {
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                },
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
                CreateAuthentication(),
                new OAuthConnectionOptions
                {
                    ClientId = "00000000-0000-0000-0000-000000000000",
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                },
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
                CreateAuthentication(),
                CreateConnection(),
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
                CreateAuthentication(),
                CreateConnection(),
                CancellationToken.None));

        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider details", exception.Message, StringComparison.Ordinal);
    }

    private static OAuthConnectionOptions CreateConnection()
        => new()
        {
            ClientId = "provider-client-id",
            AllowedOrigins = [new Uri("https://identity.example.com")],
            AdditionalScopes = ["offline_access"],
        };

    private static A2AAgentCardAuthentication CreateAuthentication(
        string tokenUrl = "https://identity.example.com/oauth/token")
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["provider"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://identity.example.com/oauth/device",
                                TokenUrl = tokenUrl,
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
                        ["provider"] = new() { List = ["profile.read"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }

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
