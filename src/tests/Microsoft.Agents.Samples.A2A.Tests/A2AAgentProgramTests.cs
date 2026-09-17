extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AAgentProgramTests
{
    [Fact]
    public void MyAgent_DeclaresOnlyGraphAndGitHubSkills()
    {
        MethodInfo[] methods = typeof(MyAgent).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);

        string[] skillNames = methods
            .SelectMany(method => method.GetCustomAttributes<A2ASkillAttribute>())
            .Select(skill => skill.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["GitHub assigned issues", "Microsoft Graph profile"], skillNames);
        Assert.Empty(methods.SelectMany(method => method.GetCustomAttributes<A2AMessageRouteAttribute>()));
        Assert.Empty(methods.SelectMany(method => method.GetCustomAttributes<EndOfConversationRouteAttribute>()));
        Assert.DoesNotContain(
            typeof(MyAgent).GetCustomAttributes<AgentInterfaceAttribute>(),
            attribute => attribute.Protocol == AgentTransportProtocol.ActivityProtocol);
    }

    [Fact]
    public async Task AnonymousAgentCardRequest_FromProgram_ReturnsAgentCard()
    {
        await using var host = await A2AAgentProcessHost.StartAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",
            ["TokenValidation__Audiences__0"] = "22222222-2222-2222-2222-222222222222",
            ["TokenValidation__TenantId"] = "11111111-1111-1111-1111-111111111111",
        });

        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using HttpResponseMessage response = await client.GetAsync("/.well-known/agent-card.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, A2AAgentAuthenticationDefaults.GitHubScheme)]
    [InlineData("", A2AAgentAuthenticationDefaults.GitHubScheme)]
    [InlineData("   ", A2AAgentAuthenticationDefaults.GitHubScheme)]
    [InlineData("Basic octocat", A2AAgentAuthenticationDefaults.GitHubScheme)]
    [InlineData("Bearer ", A2AAgentAuthenticationDefaults.GitHubScheme)]
    [InlineData("Bearer opaque-github-token", A2AAgentAuthenticationDefaults.GitHubScheme)]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJvY3RvY2F0In0.c2lnbmF0dXJl", JwtBearerDefaults.AuthenticationScheme)]
    public void BearerTokenSchemeSelector_Select_RoutesHeadersToAConcreteScheme(string? authorizationHeader, string expectedScheme)
    {
        Assert.Equal(expectedScheme, BearerTokenSchemeSelector.Select(authorizationHeader));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Basic octocat")]
    [InlineData("Bearer ")]
    [InlineData("Bearer")]
    public async Task PolicyScheme_MissingAndNonBearerHeaders_ForwardToGitHubAndReturnNoResult(string? authorizationHeader)
    {
        await using var harness = MixedBearerAuthenticationHarness.Create();

        AuthenticateResult result = await harness.AuthenticateAsync(authorizationHeader);

        Assert.True(result.None);
        Assert.Equal(0, harness.JwtAuthenticateCount);
        Assert.Equal(0, harness.GitHubRequestCount);
    }

    [Fact]
    public async Task PolicyScheme_JwtShapedBearerHeader_ForwardsToJwtBearer()
    {
        await using var harness = MixedBearerAuthenticationHarness.Create();

        AuthenticateResult result = await harness.AuthenticateAsync(
            "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJvY3RvY2F0In0.c2lnbmF0dXJl");

        Assert.True(result.Succeeded);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, result.Ticket!.AuthenticationScheme);
        Assert.Equal(1, harness.JwtAuthenticateCount);
        Assert.Equal(0, harness.GitHubRequestCount);
    }

    [Theory]
    [InlineData("Bearer two.parts")]
    [InlineData("Bearer contains+/plus.signatures")]
    public async Task PolicyScheme_MalformedBearerValues_ForwardToGitHub(string authorizationHeader)
    {
        await using var harness = MixedBearerAuthenticationHarness.Create();

        AuthenticateResult result = await harness.AuthenticateAsync(authorizationHeader);

        Assert.True(result.Succeeded);
        Assert.Equal(A2AAgentAuthenticationDefaults.GitHubScheme, result.Ticket!.AuthenticationScheme);
        Assert.Equal(0, harness.JwtAuthenticateCount);
        Assert.Equal(1, harness.GitHubRequestCount);
    }

    [Fact]
    public async Task PolicyScheme_OpaqueBearerHeader_ForwardsToGitHub()
    {
        await using var harness = MixedBearerAuthenticationHarness.Create();

        AuthenticateResult result = await harness.AuthenticateAsync("Bearer opaque-github-token");

        Assert.True(result.Succeeded);
        Assert.Equal(A2AAgentAuthenticationDefaults.GitHubScheme, result.Ticket!.AuthenticationScheme);
        Assert.Equal(0, harness.JwtAuthenticateCount);
        Assert.Equal(1, harness.GitHubRequestCount);
    }

    private sealed class A2AAgentProcessHost : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _standardOutputTask;
        private readonly Task<string> _standardErrorTask;

        private A2AAgentProcessHost(
            Process process,
            Uri baseAddress,
            Task<string> standardOutputTask,
            Task<string> standardErrorTask)
        {
            _process = process;
            BaseAddress = baseAddress;
            _standardOutputTask = standardOutputTask;
            _standardErrorTask = standardErrorTask;
        }

        public Uri BaseAddress { get; }

        public static async Task<A2AAgentProcessHost> StartAsync(Dictionary<string, string?> environment)
        {
            ArgumentNullException.ThrowIfNull(environment);

            int port = ReservePort();
            var baseAddress = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetSampleContentRoot(),
            };

            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--no-launch-profile");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(GetSampleProjectPath());

            foreach ((string key, string? value) in environment)
            {
                startInfo.Environment[key] = value ?? string.Empty;
            }

            startInfo.Environment["ASPNETCORE_URLS"] = baseAddress.GetLeftPart(UriPartial.Authority);

            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the A2AAgent sample process.");

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await WaitForAgentCardAsync(process, baseAddress, standardOutputTask, standardErrorTask).ConfigureAwait(false);
                return new A2AAgentProcessHost(process, baseAddress, standardOutputTask, standardErrorTask);
            }
            catch
            {
                await DisposeProcessAsync(process).ConfigureAwait(false);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeProcessAsync(_process).ConfigureAwait(false);
            _ = await _standardOutputTask.ConfigureAwait(false);
            _ = await _standardErrorTask.ConfigureAwait(false);
        }

        private static int ReservePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static async Task WaitForAgentCardAsync(
            Process process,
            Uri baseAddress,
            Task<string> standardOutputTask,
            Task<string> standardErrorTask)
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            try
            {
                while (true)
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException(
                            $"A2AAgent exited before serving the agent card.{Environment.NewLine}" +
                            $"STDOUT:{Environment.NewLine}{await standardOutputTask.ConfigureAwait(false)}{Environment.NewLine}" +
                            $"STDERR:{Environment.NewLine}{await standardErrorTask.ConfigureAwait(false)}");
                    }

                    try
                    {
                        using HttpResponseMessage response = await client.GetAsync(
                            "/.well-known/agent-card.json",
                            timeout.Token).ConfigureAwait(false);

                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            return;
                        }
                    }
                    catch (HttpRequestException)
                    {
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out waiting for the A2AAgent sample to listen on {baseAddress}.{Environment.NewLine}" +
                    $"STDOUT:{Environment.NewLine}{await standardOutputTask.ConfigureAwait(false)}{Environment.NewLine}" +
                    $"STDERR:{Environment.NewLine}{await standardErrorTask.ConfigureAwait(false)}");
            }
        }

        private static async Task DisposeProcessAsync(Process process)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }

            process.Dispose();
        }

        private static string GetSampleProjectPath()
        {
            return Path.Combine(GetSampleContentRoot(), "A2AAgent.csproj");
        }

        private static string GetSampleContentRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "src", "samples", "A2A", "A2AAgent");
                if (File.Exists(Path.Combine(candidate, "A2AAgent.csproj")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not locate the A2AAgent sample content root.");
        }
    }

    private sealed class MixedBearerAuthenticationHarness : IAsyncDisposable
    {
        private readonly CountingGitHubApiHandler _gitHubApiHandler;
        private readonly JwtInvocationCounter _jwtCounter;
        private readonly ServiceProvider _services;

        private MixedBearerAuthenticationHarness(
            ServiceProvider services,
            CountingGitHubApiHandler gitHubApiHandler,
            JwtInvocationCounter jwtCounter)
        {
            _services = services;
            _gitHubApiHandler = gitHubApiHandler;
            _jwtCounter = jwtCounter;
        }

        public int GitHubRequestCount => _gitHubApiHandler.RequestCount;

        public int JwtAuthenticateCount => _jwtCounter.AuthenticateCount;

        public static MixedBearerAuthenticationHarness Create()
        {
            var gitHubApiHandler = new CountingGitHubApiHandler();
            var jwtCounter = new JwtInvocationCounter();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(jwtCounter);
            services.AddHttpClient(A2AAgentAuthenticationDefaults.GitHubHttpClientName)
                .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://api.github.com/"))
                .ConfigurePrimaryHttpMessageHandler(() => gitHubApiHandler);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = A2AAgentAuthenticationDefaults.PolicyScheme;
                options.DefaultChallengeScheme = A2AAgentAuthenticationDefaults.PolicyScheme;
            })
            .AddPolicyScheme(
                A2AAgentAuthenticationDefaults.PolicyScheme,
                displayName: null,
                options => options.ForwardDefaultSelector = context => BearerTokenSchemeSelector.Select(context.Request.Headers.Authorization.ToString()))
            .AddScheme<AuthenticationSchemeOptions, GitHubAuthenticationHandler>(
                A2AAgentAuthenticationDefaults.GitHubScheme,
                _ => { })
            .AddScheme<AuthenticationSchemeOptions, RecordingJwtBearerHandler>(
                JwtBearerDefaults.AuthenticationScheme,
                _ => { });

            return new MixedBearerAuthenticationHarness(services.BuildServiceProvider(), gitHubApiHandler, jwtCounter);
        }

        public async Task<AuthenticateResult> AuthenticateAsync(string? authorizationHeader)
        {
            var context = new DefaultHttpContext { RequestServices = _services };
            if (authorizationHeader != null)
            {
                context.Request.Headers.Authorization = authorizationHeader;
            }

            return await _services
                .GetRequiredService<IAuthenticationService>()
                .AuthenticateAsync(context, A2AAgentAuthenticationDefaults.PolicyScheme);
        }

        public ValueTask DisposeAsync() => _services.DisposeAsync();
    }

    private sealed class JwtInvocationCounter
    {
        public int AuthenticateCount { get; set; }
    }

    private sealed class RecordingJwtBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtInvocationCounter invocationCounter)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            invocationCounter.AuthenticateCount++;
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "jwt-caller")], Scheme.Name));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name)));
        }
    }

    private sealed class CountingGitHubApiHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": 42, "login": "octocat", "name": "The Octocat" }""", Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("X-OAuth-Scopes", "repo");
            return Task.FromResult(response);
        }
    }
}
