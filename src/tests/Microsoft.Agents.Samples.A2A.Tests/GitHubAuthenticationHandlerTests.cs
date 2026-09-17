extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GitHubAuthenticationHandlerTests
{
    private const string TestAccessToken = "test-github-token";

    [Fact]
    public async Task HandleAuthenticateAsync_ValidOpaqueTokenWithRepoScope_ReturnsTicketAndStoresAccessToken()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            user: CreateUser(),
            oauthScopes: ["read:user", "repo", "user:email"]);

        AuthenticateResult result = await harness.AuthenticateAsync($"Bearer {TestAccessToken}");

        Assert.True(result.Succeeded);
        Assert.Equal(TestAccessToken, result.Ticket!.Properties.GetTokenValue("access_token"));
        Assert.Equal("octocat", result.Principal!.FindFirst("urn:github:login")!.Value);
        Assert.Equal(TestAccessToken, harness.LastAccessToken);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingRepoScope_Fails()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            user: CreateUser(),
            oauthScopes: ["read:user", "user:email"]);

        AuthenticateResult result = await harness.AuthenticateAsync($"Bearer {TestAccessToken}");

        Assert.False(result.Succeeded);
        Assert.Contains("repo scope", result.Failure!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_GitHubApiException_ReturnsFailWithoutLeakingToken()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            currentException: new ApiException("Bad gateway", HttpStatusCode.BadGateway));

        AuthenticateResult result = await harness.AuthenticateAsync($"Bearer {TestAccessToken}");

        AssertExplicitValidationFailure(result);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_GitHubTransportFailure_ReturnsFailWithoutLeakingToken()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            currentException: new HttpRequestException("Bad gateway"));

        AuthenticateResult result = await harness.AuthenticateAsync($"Bearer {TestAccessToken}");

        AssertExplicitValidationFailure(result);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_GitHubValidationCancellation_Propagates()
    {
        await using var harness = GitHubAuthenticationHarness.Create(
            currentException: new OperationCanceledException("Cancelled by caller"));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => harness.AuthenticateAsync($"Bearer {TestAccessToken}"));
    }

    private static void AssertExplicitValidationFailure(AuthenticateResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Contains("GitHub token validation failed", result.Failure!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(TestAccessToken, result.Failure.Message, StringComparison.Ordinal);
    }

    private sealed class GitHubAuthenticationHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly RecordingGitHubClientFactory _gitHubClients;

        private GitHubAuthenticationHarness(ServiceProvider services, RecordingGitHubClientFactory gitHubClients)
        {
            _services = services;
            _gitHubClients = gitHubClients;
        }

        public string? LastAccessToken => _gitHubClients.LastAccessToken;

        public static GitHubAuthenticationHarness Create(
            User? user = null,
            IReadOnlyList<string>? oauthScopes = null,
            Exception? currentException = null)
        {
            var gitHubClients = new RecordingGitHubClientFactory(
                user ?? CreateUser(),
                oauthScopes ?? ["repo"],
                currentException);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGitHubClientFactory>(gitHubClients);
            services.AddAuthentication(A2AAgentAuthenticationDefaults.GitHubScheme)
                .AddScheme<AuthenticationSchemeOptions, GitHubAuthenticationHandler>(A2AAgentAuthenticationDefaults.GitHubScheme, _ => { });
            return new GitHubAuthenticationHarness(services.BuildServiceProvider(), gitHubClients);
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

    private sealed class RecordingGitHubClientFactory(
        User user,
        IReadOnlyList<string> oauthScopes,
        Exception? currentException) : IGitHubClientFactory
    {
        public string? LastAccessToken { get; private set; }

        public IGitHubClient Create(string accessToken)
        {
            LastAccessToken = accessToken;

            var users = new Mock<IUsersClient>(MockBehavior.Strict);
            if (currentException is null)
            {
                users.Setup(client => client.Current())
                    .ReturnsAsync(user);
            }
            else
            {
                users.Setup(client => client.Current())
                    .ThrowsAsync(currentException);
            }

            var client = new Mock<IGitHubClient>(MockBehavior.Strict);
            client.SetupGet(mock => mock.User)
                .Returns(users.Object);
            client.Setup(mock => mock.GetLastApiInfo())
                .Returns(CreateApiInfo(oauthScopes));

            return client.Object;
        }
    }

    private static ApiInfo CreateApiInfo(IReadOnlyList<string> oauthScopes)
        => new(
            new Dictionary<string, Uri>(),
            new List<string>(oauthScopes),
            new List<string>(),
            string.Empty,
            rateLimit: null);

    private static User CreateUser()
        => new(
            avatarUrl: "https://avatars.githubusercontent.com/u/42?v=4",
            bio: string.Empty,
            blog: string.Empty,
            collaborators: 0,
            company: string.Empty,
            createdAt: DateTimeOffset.UnixEpoch,
            updatedAt: DateTimeOffset.UnixEpoch,
            diskUsage: 0,
            email: string.Empty,
            followers: 0,
            following: 0,
            hireable: null,
            htmlUrl: "https://github.com/octocat",
            totalPrivateRepos: 0,
            id: 42,
            location: string.Empty,
            login: "octocat",
            name: "The Octocat",
            nodeId: "MDQ6VXNlcjQy",
            ownedPrivateRepos: 0,
            plan: null,
            privateGists: 0,
            publicGists: 0,
            publicRepos: 0,
            url: "https://api.github.com/users/octocat",
            permissions: null,
            siteAdmin: false,
            ldapDistinguishedName: string.Empty,
            suspendedAt: null);
}
