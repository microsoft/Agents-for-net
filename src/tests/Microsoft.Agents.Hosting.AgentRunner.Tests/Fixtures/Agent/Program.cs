// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RunnerFixture;

public static class Program
{
    public static void RegisterServices(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsProduction())
        {
            throw new System.InvalidOperationException("The runner must register services in Production.");
        }

        builder.Services.AddSingleton<IAgent, TestAgent>();
        builder.Services.AddSingleton<IAgentHttpAdapter, TestAdapter>();
        builder.Services.AddSingleton<DependencyMarker>();

        // Only this test fixture supports local signing metadata for container smoke tests.
        var metadataPath = builder.Configuration["AgentRunnerFixtureSigningKeyPath"];
        if (!string.IsNullOrEmpty(metadataPath))
        {
            var configuration = new OpenIdConnectConfiguration();
            configuration.SigningKeys.Add(new JsonWebKey(File.ReadAllText(metadataPath)));
            builder.Services.PostConfigureAll<JwtBearerOptions>(options =>
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration));
        }
    }

    private sealed class TestAgent : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestAdapter(DependencyMarker marker) : IAgentHttpAdapter
    {
        public async Task ProcessAsync(
            HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken)
        {
            httpResponse.StatusCode = StatusCodes.Status202Accepted;
            await httpResponse.WriteAsync(marker.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}
