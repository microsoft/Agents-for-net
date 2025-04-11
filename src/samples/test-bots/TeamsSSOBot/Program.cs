// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.App.UserAuth;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Threading.Tasks;
using TeamsSSOBot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.Services.AddSingleton(sp =>
{
    IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(config.AAD_APP_CLIENT_ID)
                                        .WithClientSecret(config.AAD_APP_CLIENT_SECRET)
                                        .WithTenantId(config.AAD_APP_TENANT_ID)
                                        .WithLegacyCacheCompatibility(false)
                                        .Build();
    app.AddInMemoryTokenCache(); // For development purpose only, use distributed cache in production environment
    return app;
});

// Add AgentApplicationOptions
builder.Services.AddTransient(sp =>
{
    var adapter = sp.GetService<IChannelAdapter>();
    var storage = sp.GetService<IStorage>();
    IConfidentialClientApplication msal = sp.GetService<IConfidentialClientApplication>();
    string signInLink = $"https://{config.BOT_DOMAIN}/auth-start.html";

    var authOptions = new UserAuthorizationOptions(
        sp.GetService<IConnections>(), 
        new TeamsSsoAuthentication(
            "graph",
            new TeamsSsoSettings(["User.Read"], signInLink, msal),
            storage))
    {
        // Auto-SignIn will use this OAuth flow
        DefaultHandlerName = "graph",
        AutoSignIn = UserAuthorizationOptions.AutoSignInOn
    };

    return new AgentApplicationOptions()
    {
        Adapter = adapter,
        StartTypingTimer = false,
        TurnStateFactory = () => new TurnState(storage),
        UserAuthorization = authOptions
    };
});

// Add the bot (which is transient)
builder.AddAgent<AuthBot>();

