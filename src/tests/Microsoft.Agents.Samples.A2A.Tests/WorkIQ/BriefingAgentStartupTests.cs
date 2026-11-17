extern alias WorkIQBriefingSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorkIQBriefingSample::WorkIQBriefingAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class BriefingAgentStartupTests
{
    [Fact]
    public async Task Startup_AdvertisesTheSkillButProtectsTaskOperationsEvenInDevelopment()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var settings = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "WorkIQBriefingAgent.appsettings.json")))
            .Replace("{{TenantId}}", "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal)
            .Replace("{{ClientId}}", "22222222-2222-2222-2222-222222222222", StringComparison.Ordinal);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(settings));
        builder.Configuration.AddJsonStream(json);
        builder.Configuration["Connections:ServiceConnection:Settings:ClientSecret"] = "test-only-placeholder";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        BriefingAgentStartup.ConfigureBuilder(builder);
        await using var app = builder.Build();
        BriefingAgentStartup.ConfigureApplication(app);
        await app.StartAsync(timeout.Token);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var cardResponse = await client.GetAsync("/.well-known/agent-card.json", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, cardResponse.StatusCode);
        var card = JsonSerializer.Deserialize<AgentCard>(await cardResponse.Content.ReadAsStringAsync(timeout.Token), A2AJsonUtilities.DefaultOptions)!;
        var skill = Assert.Single(card.Skills);
        Assert.NotEmpty(skill.SecurityRequirements!);
        Assert.Contains("delegated", card.SecuritySchemes!.Keys);

        using var unauthorized = await client.PostAsync("/a2a", new StringContent("{}", Encoding.UTF8, "application/json"), timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await app.StopAsync(timeout.Token);
    }
}
