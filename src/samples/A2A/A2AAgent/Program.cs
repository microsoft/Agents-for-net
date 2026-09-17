// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2AAgent;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(
        ConfigureAuthentication,
        forceEnable: ShouldEnableTokenValidation(builder.Configuration, builder.Environment));

builder.Services.AddHttpClient<IGraphProfileClient, GraphProfileClient>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
});
builder.Services.AddHttpClient(A2AAgentAuthenticationDefaults.GitHubHttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("agents-sdk-net-a2a-sample");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
});
builder.Services.AddHttpClient<IGitHubIssuesClient, GitHubIssuesClient>(
    A2AAgentAuthenticationDefaults.GitHubHttpClientName);

builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAgents();
app.MapDefaultAgentEndpoints();
app.MapA2AApplicationEndpoints(requireAuth: false);

app.Run();

static void ConfigureAuthentication(IHostApplicationBuilder builder)
{
    builder.AddAgentAspNetAuthentication(A2AAgentAuthenticationDefaults.TokenValidationSectionName);
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = A2AAgentAuthenticationDefaults.PolicyScheme;
        })
        .AddPolicyScheme(
            A2AAgentAuthenticationDefaults.PolicyScheme,
            displayName: null,
            options =>
            {
                options.ForwardDefaultSelector = context =>
                    BearerTokenSchemeSelector.Select(context.Request.Headers.Authorization);
            })
        .AddScheme<AuthenticationSchemeOptions, GitHubAuthenticationHandler>(
            A2AAgentAuthenticationDefaults.GitHubScheme,
            _ => { });
}

static bool ShouldEnableTokenValidation(IConfiguration configuration, IHostEnvironment environment) =>
    !environment.IsDevelopment() || IsTokenValidationConfigured(configuration);

static bool IsTokenValidationConfigured(IConfiguration configuration)
{
    IConfigurationSection tokenValidation = configuration.GetSection(A2AAgentAuthenticationDefaults.TokenValidationSectionName);
    if (!tokenValidation.Exists())
    {
        return false;
    }

    string? tenantId = tokenValidation["TenantId"];

    if (string.IsNullOrWhiteSpace(tenantId)
        || string.Equals(tenantId, "{{TenantId}}", StringComparison.Ordinal)
        || string.Equals(tenantId, "<tenant-id>", StringComparison.Ordinal)
        || !Guid.TryParse(tenantId, out _))
    {
        return false;
    }

    bool hasAudience = false;
    foreach (IConfigurationSection audience in tokenValidation.GetSection("Audiences").GetChildren())
    {
        hasAudience = true;

        if (string.IsNullOrWhiteSpace(audience.Value)
            || string.Equals(audience.Value, "{{ClientId}}", StringComparison.Ordinal)
            || string.Equals(audience.Value, "<client-id>", StringComparison.Ordinal)
            || !Guid.TryParse(audience.Value, out _))
        {
            return false;
        }
    }

    return hasAudience;
}
