extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GitHubAuthenticationHandlerTests
{
    [Fact]
    public async Task HandleAuthenticateAsync_ValidOpaqueTokenWithRepoScope_ReturnsTicketAndStoresAccessToken()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            statusCode: HttpStatusCode.OK,
            responseBody: """{ "id": 42, "login": "octocat", "name": "The Octocat" }""",
            scopesHeader: "read:user, repo, user:email");

        AuthenticateResult result = await harness.AuthenticateAsync("Bearer github_pat_test");

        Assert.True(result.Succeeded);
        Assert.Equal("github_pat_test", result.Ticket!.Properties.GetTokenValue("access_token"));
        Assert.Equal("octocat", result.Principal!.FindFirst("urn:github:login")!.Value);
        RequestRecord request = harness.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/user", request.PathAndQuery);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("github_pat_test", request.AuthorizationParameter);
        Assert.Equal("application/vnd.github+json", request.Accept);
        Assert.Equal("agents-sdk-net-a2a-sample", request.UserAgent);
        Assert.Equal("2022-11-28", request.ApiVersion);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingRepoScope_Fails()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            statusCode: HttpStatusCode.OK,
            responseBody: """{ "id": 42, "login": "octocat", "name": "The Octocat" }""",
            scopesHeader: "read:user, user:email");

        AuthenticateResult result = await harness.AuthenticateAsync("Bearer github_pat_test");

        Assert.False(result.Succeeded);
        Assert.Contains("repo scope", result.Failure!.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed class GitHubAuthenticationHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly StubGitHubApiHandler _handler;

        private GitHubAuthenticationHarness(ServiceProvider services, StubGitHubApiHandler handler)
        {
            _services = services;
            _handler = handler;
        }

        public RequestRecord? LastRequest => _handler.LastRequest;

        public static GitHubAuthenticationHarness Create(HttpStatusCode statusCode, string responseBody, string scopesHeader)
        {
            var handler = new StubGitHubApiHandler(statusCode, responseBody, scopesHeader);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient(A2AAgentAuthenticationDefaults.GitHubHttpClientName)
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("https://api.github.com/");
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("agents-sdk-net-a2a-sample");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                })
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddAuthentication(A2AAgentAuthenticationDefaults.GitHubScheme)
                .AddScheme<AuthenticationSchemeOptions, GitHubAuthenticationHandler>(A2AAgentAuthenticationDefaults.GitHubScheme, _ => { });
            return new GitHubAuthenticationHarness(services.BuildServiceProvider(), handler);
        }

        public async Task<AuthenticateResult> AuthenticateAsync(string authorizationHeader)
        {
            var context = new DefaultHttpContext { RequestServices = _services };
            context.Request.Headers.Authorization = authorizationHeader;
            return await _services
                .GetRequiredService<IAuthenticationService>()
                .AuthenticateAsync(context, A2AAgentAuthenticationDefaults.GitHubScheme);
        }

        public ValueTask DisposeAsync() => _services.DisposeAsync();
    }

    private sealed class StubGitHubApiHandler(HttpStatusCode statusCode, string responseBody, string scopesHeader) : HttpMessageHandler
    {
        public RequestRecord? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = new RequestRecord(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.Accept.SingleOrDefault()?.MediaType,
                request.Headers.UserAgent.ToString(),
                request.Headers.TryGetValues("X-GitHub-Api-Version", out IEnumerable<string>? versions)
                    ? versions.Single()
                    : null);
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(scopesHeader))
            {
                response.Headers.Add("X-OAuth-Scopes", scopesHeader);
            }

            return Task.FromResult(response);
        }
    }

    private sealed record RequestRecord(
        HttpMethod Method,
        string PathAndQuery,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? Accept,
        string UserAgent,
        string? ApiVersion);
}
