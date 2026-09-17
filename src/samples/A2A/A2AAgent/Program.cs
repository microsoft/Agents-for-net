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
bool tokenValidationEnabled = ShouldEnableTokenValidation(builder.Configuration, builder.Environment);

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(
        Program.ConfigureAuthentication,
        forceEnable: tokenValidationEnabled);

builder.Services.AddHttpClient<IGraphProfileClient, GraphProfileClient>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
});
builder.Services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
builder.Services.AddSingleton<IGitHubIssuesClient, GitHubIssuesClient>();

builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.UseAuthentication();
if (tokenValidationEnabled)
{
    app.Use(async (context, next) =>
    {
        string authorizationHeader = context.Request.Headers.Authorization.ToString();
        if (IsBearerHeader(authorizationHeader)
            && context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync().ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    });
}

app.UseAuthorization();
app.UseAgents();
app.MapDefaultAgentEndpoints();
app.MapA2AApplicationEndpoints(requireAuth: false);

app.Run();

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

static bool IsBearerHeader(string authorizationHeader)
{
    const string BearerScheme = "Bearer";
    return authorizationHeader.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase)
        && (authorizationHeader.Length == BearerScheme.Length
            || char.IsWhiteSpace(authorizationHeader[BearerScheme.Length]));
}

internal partial class Program
{
    internal static void ConfigureAuthentication(IHostApplicationBuilder builder)
    {
        builder.AddAgentAspNetAuthentication(A2AAgentAuthenticationDefaults.TokenValidationSectionName);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = A2AAgentAuthenticationDefaults.PolicyScheme;
                options.DefaultAuthenticateScheme = A2AAgentAuthenticationDefaults.PolicyScheme;
                options.DefaultChallengeScheme = A2AAgentAuthenticationDefaults.PolicyScheme;
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
}
