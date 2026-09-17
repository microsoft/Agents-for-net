// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GitHubDeviceFlowTokenClientTests
{
    [Fact]
    public async Task AcquireTokenAsync_WaitsAdvertisedIntervalBeforeFirstPollAndBeforeEachSubsequentPoll()
    {
        var delays = new List<TimeSpan>();
        var events = new List<string>();
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            new[]
            {
                """{ "error": "authorization_pending" }""",
                """{ "access_token": "github-token", "token_type": "bearer", "scope": "repo" }""",
            },
            onRequest: request => events.Add(request.RequestUri!.AbsolutePath)));
        var output = new StringWriter();
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            output,
            (delay, _) =>
            {
                delays.Add(delay);
                events.Add($"delay:{delay.TotalSeconds:0}");
                return Task.CompletedTask;
            });

        GitHubDeviceFlowAccessToken token = await sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None);

        Assert.Equal("github-token", token.AccessToken);
        Assert.Null(token.ExpiresIn);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)], delays);
        Assert.Equal(
            ["/login/device/code", "delay:5", "/login/oauth/access_token", "delay:5", "/login/oauth/access_token"],
            events);
        Assert.Contains("https://github.com/login/device", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("ABCD-EFGH", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("github-token", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcquireTokenAsync_OmittedInterval_DefaultsToFiveSeconds()
    {
        var delays = new List<TimeSpan>();
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""",
            [""" { "access_token": "github-token", "token_type": "bearer", "scope": "repo" } """]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        GitHubDeviceFlowAccessToken token = await sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None);

        Assert.Equal("github-token", token.AccessToken);
        Assert.Null(token.ExpiresIn);
        Assert.Equal([TimeSpan.FromSeconds(5)], delays);
    }

    [Fact]
    public async Task AcquireTokenAsync_TokenExpiration_IsReturnedToTheCache()
    {
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""",
            [""" { "access_token": "github-token", "token_type": "bearer", "scope": "repo", "expires_in": 28800 } """]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        GitHubDeviceFlowAccessToken token = await sut.AcquireTokenAsync(
            CreateGitHubAuthentication("repo"),
            CancellationToken.None);

        Assert.Equal("github-token", token.AccessToken);
        Assert.Equal(TimeSpan.FromHours(8), token.ExpiresIn);
    }

    [Theory]
    [InlineData("""{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 0 }""")]
    [InlineData("""{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": -1 }""")]
    public async Task AcquireTokenAsync_ExplicitNonPositiveInterval_Throws(string deviceResponseJson)
    {
        var delays = new List<TimeSpan>();
        var handler = new SequenceJsonHandler(deviceResponseJson, ["""{"access_token":"ignored"}"""]);
        using var client = new HttpClient(handler);
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains("interval", exception.Message, StringComparison.Ordinal);
        Assert.Contains("greater than zero", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task AcquireTokenAsync_PostsFormUrlEncodedRequestsAndRequestsJson()
    {
        var handler = new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            [""" { "access_token": "github-token", "token_type": "bearer", "scope": "repo read:org" } """]);
        using var client = new HttpClient(handler);
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        GitHubDeviceFlowAccessToken token = await sut.AcquireTokenAsync(CreateGitHubAuthentication("repo", "read:org"), CancellationToken.None);

        Assert.Equal("github-token", token.AccessToken);
        Assert.Null(token.ExpiresIn);
        Assert.Collection(
            handler.Requests,
            deviceRequest =>
            {
                Assert.Equal(HttpMethod.Post, deviceRequest.Method);
                Assert.Equal("https://github.com/login/device/code", deviceRequest.RequestUri?.AbsoluteUri);
                Assert.Equal("application/json", deviceRequest.Accept);
                Assert.Equal("application/x-www-form-urlencoded", deviceRequest.ContentType);
                Assert.Contains("client_id=Iv1.1234567890abcdef", deviceRequest.Content, StringComparison.Ordinal);
                Assert.Contains("scope=repo+read%3Aorg", deviceRequest.Content, StringComparison.Ordinal);
            },
            tokenRequest =>
            {
                Assert.Equal(HttpMethod.Post, tokenRequest.Method);
                Assert.Equal("https://github.com/login/oauth/access_token", tokenRequest.RequestUri?.AbsoluteUri);
                Assert.Equal("application/json", tokenRequest.Accept);
                Assert.Equal("application/x-www-form-urlencoded", tokenRequest.ContentType);
                Assert.Contains("client_id=Iv1.1234567890abcdef", tokenRequest.Content, StringComparison.Ordinal);
                Assert.Contains("device_code=device-code", tokenRequest.Content, StringComparison.Ordinal);
                Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", tokenRequest.Content, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task AcquireTokenAsync_AuthorizationPendingAndSlowDown_UseExpectedPollingIntervals()
    {
        var delays = new List<TimeSpan>();
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            new[]
            {
                """{ "error": "authorization_pending" }""",
                """{ "error": "slow_down" }""",
                """{ "error": "authorization_pending" }""",
                """{ "access_token": "github-token", "token_type": "bearer", "scope": "repo" }""",
            }));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        GitHubDeviceFlowAccessToken token = await sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None);

        Assert.Equal("github-token", token.AccessToken);
        Assert.Null(token.ExpiresIn);
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)],
            delays);
    }

    [Fact]
    public async Task AcquireTokenAsync_TimeoutDuringTokenPoll_RetriesWithTemporaryExponentialBackoff()
    {
        var delays = new List<TimeSpan>();
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            new[]
            {
                """{ "error": "authorization_pending" }""",
                """{ "access_token": "github-token", "token_type": "bearer", "scope": "repo" }""",
            },
            tokenExceptionFactory: static (attempt, cancellationToken) => attempt == 0
                ? new OperationCanceledException("The request timed out.", new TimeoutException("The operation timed out."), cancellationToken)
                : null));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        GitHubDeviceFlowAccessToken token = await sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None);

        Assert.Equal("github-token", token.AccessToken);
        Assert.Null(token.ExpiresIn);
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)],
            delays);
    }

    [Theory]
    [InlineData("expired_token", "expired")]
    [InlineData("access_denied", "denied")]
    public async Task AcquireTokenAsync_TerminalTokenErrors_ThrowInvalidOperationException(string error, string expectedText)
    {
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            [$@"{{ ""error"": ""{error}"", ""error_description"": ""{error} details"" }}"]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains(expectedText, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(error, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcquireTokenAsync_CallerCancellationDuringTokenPoll_PropagatesUnchanged()
    {
        var delays = new List<TimeSpan>();
        using var cancellationTokenSource = new CancellationTokenSource();
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            [""" { "access_token": "ignored" } """],
            tokenExceptionFactory: (attempt, cancellationToken) =>
            {
                if (attempt != 0)
                {
                    return null;
                }

                cancellationTokenSource.Cancel();
                return new OperationCanceledException("Caller canceled the poll.", cancellationToken);
            }));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        Assert.Equal([TimeSpan.FromSeconds(5)], delays);
    }

    [Theory]
    [InlineData("""{ "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""", "device_code")]
    [InlineData("""{ "device_code": "device-code", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""", "user_code")]
    [InlineData("""{ "device_code": "device-code", "user_code": "ABCD-EFGH", "expires_in": 900, "interval": 5 }""", "verification_uri")]
    [InlineData("""{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "interval": 5 }""", "expires_in")]
    public async Task AcquireTokenAsync_MissingDeviceAuthorizationPayloadMember_Throws(string deviceResponseJson, string expectedMember)
    {
        using var client = new HttpClient(new SequenceJsonHandler(deviceResponseJson, ["""{"access_token":"ignored"}"""]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains(expectedMember, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-json", true)]
    [InlineData("""{ "error": "authorization_pending", """, false)]
    public async Task AcquireTokenAsync_MalformedJsonPayload_Throws(string json, bool deviceResponse)
    {
        using var client = new HttpClient(deviceResponse
            ? new SequenceJsonHandler(json, ["""{"access_token":"ignored"}"""])
            : new SequenceJsonHandler(
                """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
                [json]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains("JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{}""", "missing access token")]
    [InlineData("""{ "access_token": "" }""", "missing access token")]
    [InlineData("""{ "error": "unsupported_grant_type", "error_description": "bad grant" }""", "unsupported_grant_type")]
    public async Task AcquireTokenAsync_InvalidTokenPayload_Throws(string tokenResponseJson, string expectedText)
    {
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            [tokenResponseJson]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains(expectedText, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "device authorization endpoint")]
    [InlineData(HttpStatusCode.InternalServerError, "device authorization endpoint")]
    public async Task AcquireTokenAsync_DeviceEndpointHttpFailure_Throws(HttpStatusCode statusCode, string expectedText)
    {
        using var client = new HttpClient(new StatusCodeSequenceHandler(statusCode));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains(expectedText, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(((int)statusCode).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcquireTokenAsync_TokenEndpointHttpFailure_Throws()
    {
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            ["""{"error":"server_error"}"""],
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains("token endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("502", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcquireTokenAsync_MissingGitHubClientId_Throws()
    {
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            ["""{"access_token":"ignored"}"""]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions(),
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthentication("repo"), CancellationToken.None));

        Assert.Contains(nameof(A2AClientAuthenticationOptions.GitHubClientId), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("None", "delegated")]
    [InlineData("App", "delegated")]
    [InlineData("Delegated", "application")]
    public async Task AcquireTokenAsync_UnsupportedAuthentication_Throws(string modeName, string flowKind)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            ["""{"access_token":"ignored"}"""]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateNonGitHubAuthentication(mode, flowKind), CancellationToken.None));

        Assert.Contains("GitHub", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://github.com:444/login/device/code", "https://github.com/login/oauth/access_token")]
    [InlineData("https://github.com/login/device/code", "https://github.com:444/login/oauth/access_token")]
    public async Task AcquireTokenAsync_NonDefaultGitHubPort_Throws(string deviceAuthorizationUrl, string tokenUrl)
    {
        using var client = new HttpClient(new SequenceJsonHandler(
            """{ "device_code": "device-code", "user_code": "ABCD-EFGH", "verification_uri": "https://github.com/login/device", "expires_in": 900, "interval": 5 }""",
            ["""{"access_token":"ignored"}"""]));
        var sut = new GitHubDeviceFlowTokenClient(
            client,
            new A2AClientAuthenticationOptions { GitHubClientId = "Iv1.1234567890abcdef" },
            TextWriter.Null,
            static (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AcquireTokenAsync(CreateGitHubAuthenticationWithEndpoints(deviceAuthorizationUrl, tokenUrl, "repo"), CancellationToken.None));

        Assert.Contains("https://github.com/login", exception.Message, StringComparison.Ordinal);
    }

    private static A2AAgentCardAuthentication CreateGitHubAuthentication(params string[] scopes)
        => CreateGitHubAuthenticationWithEndpoints(
            "https://github.com/login/device/code",
            "https://github.com/login/oauth/access_token",
            scopes);

    private static A2AAgentCardAuthentication CreateGitHubAuthenticationWithEndpoints(
        string deviceAuthorizationUrl,
        string tokenUrl,
        params string[] scopes)
    {
        AgentCard card = new()
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["github"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = deviceAuthorizationUrl,
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
                        ["github"] = new() { List = scopes.ToList() },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }

    private static A2AAgentCardAuthentication CreateNonGitHubAuthentication(A2AAuthMode mode, string flowKind)
    {
        string schemeName = mode == A2AAuthMode.App ? "application" : "delegated";
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [schemeName] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = flowKind == "delegated"
                                ? new()
                                {
                                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                                }
                                : null,
                            ClientCredentials = flowKind == "application"
                                ? new()
                                {
                                    TokenUrl = "https://github.com/login/oauth/access_token",
                                }
                                : null,
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
                        [schemeName] = new() { List = ["repo"] },
                    },
                },
            ],
        };

        return flowKind == "application"
            ? A2AAgentCardAuthentication.Select(card, A2AAuthMode.App)
            : A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }

    private sealed class SequenceJsonHandler(
        string deviceJson,
        IReadOnlyList<string> tokenJson,
        HttpStatusCode deviceStatusCode = HttpStatusCode.OK,
        HttpStatusCode tokenStatusCode = HttpStatusCode.OK,
        Func<int, CancellationToken, Exception?>? tokenExceptionFactory = null,
        Action<RequestRecord>? onRequest = null) : HttpMessageHandler
    {
        private int _callIndex;
        private int _tokenResponseIndex;

        public List<RequestRecord> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestRecord record = await RequestRecord.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            Requests.Add(record);
            onRequest?.Invoke(record);

            int index = _callIndex++;
            bool isDeviceRequest = index == 0;
            if (!isDeviceRequest && tokenExceptionFactory?.Invoke(index - 1, cancellationToken) is Exception exception)
            {
                throw exception;
            }

            HttpStatusCode statusCode = isDeviceRequest ? deviceStatusCode : tokenStatusCode;
            string json = isDeviceRequest
                ? deviceJson
                : tokenJson[Math.Min(_tokenResponseIndex++, tokenJson.Count - 1)];

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StatusCodeSequenceHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("""{ "error": "bad_request" }""", Encoding.UTF8, "application/json"),
            });
    }

    private sealed record RequestRecord(
        HttpMethod Method,
        Uri? RequestUri,
        string Accept,
        string? ContentType,
        string Content)
    {
        public static async Task<RequestRecord> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string accept = request.Headers.Accept.SingleOrDefault()?.MediaType ?? string.Empty;

            return new RequestRecord(
                request.Method,
                request.RequestUri,
                accept,
                request.Content?.Headers.ContentType?.MediaType,
                content);
        }
    }
}
