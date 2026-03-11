// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CodeFirstAuth;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var clientId = builder.Configuration["AGENT_CLIENTID"] ?? throw new ArgumentException("Missing AGENT_CLIENTID in env");
var tenantId = builder.Configuration["AGENT_TENANTID"] ?? throw new ArgumentException("Missing AGENT_TENANTID in env");
var clientSecret = builder.Configuration["AGENT_CLIENTSECRET"] ?? throw new ArgumentException("Missing AGENT_CLIENTSECRET in env");

// Build IConnections
builder.Services.AddSingleton<IConnections>(sp =>
{
    return new ConfigurationConnections(
        new Dictionary<string, IAccessTokenProvider> {
            {
                "ServiceConnection",
                new MsalAuth(sp, new ConnectionSettings()
                {
                    AuthType = AuthTypes.ClientSecret,
                    ClientSecret = clientSecret,
                    Authority = $"https://login.microsoftonline.com/{tenantId}",
                    ClientId = clientId,
                    Scopes = [AuthenticationConstants.BotFrameworkDefaultScope]
                })
            }
        },
        [
            new ConnectionMapItem() { ServiceUrl = "*", Connection = "ServiceConnection" }
        ]
    );
});

// Build AgentApplicationOptions
builder.Services.AddSingleton(sp =>
{
    var storage = sp.GetRequiredService<IStorage>();
    var connections = sp.GetRequiredService<IConnections>();

    // List of OAuth Handlers
    IUserAuthorization[] handlers = [
        new AzureBotUserAuthorization(
            "me",
            storage,
            connections,
            new OAuthSettings()
            {
                AzureBotOAuthConnectionName = "graph",
                Title = "SigIn for Me",
                Text = "Please sign in and send the 6-digit code"
            }
        )
    ];

    return new AgentApplicationOptions(storage)
    {
        StartTypingTimer = true,
        NormalizeMentions = false,
        RemoveRecipientMention = false,
        UserAuthorization = new(sp.GetRequiredService<ILoggerFactory>(), storage, connections, handlers)
        {
            DefaultHandlerName = "me",
            AutoSignIn = UserAuthorizationOptions.AutoSignInOff,
        }
    };
});

// Add the AgentApplication
builder.AddAgent<AuthAgent>();


// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.
builder.Services.AddControllers();

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddAgentAspNetAuthentication(new AspNetExtensions.TokenValidationOptions()
    {
        Audiences = [clientId],
        TenantId = tenantId
    });
}

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
