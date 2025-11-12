// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AutoSignIn;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Threading;
using static FullAuthentication.AspNetExtensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var clientId = "{{redacted-but-use-env-anyway}}";
var tenantId = "{{redacted-but-use-env-anyway}}";

// Register Connections
builder.Services.AddSingleton<IConnections>(sp =>
{
    return new ConfigurationConnections(
        new Dictionary<string, IAccessTokenProvider> { 
            { 
                "ServiceConnection", 
                new MsalAuth(sp, new ConnectionSettings()
                {
                    AuthType = AuthTypes.ClientSecret,
                    ClientSecret = "{{redacted-but-use-env-anyway}}",
                    Authority = $"https://login.microsoftonline.com/{tenantId}",
                    ClientId = clientId,
                    Scopes = [$"{AuthenticationConstants.BotFrameworkScope}/.default"]   // should add/change a constant for this.
                })
            }
        },
        [
            new ConnectionMapItem() { ServiceUrl = "*", Connection = "ServiceConnection" }
        ]
    );
});

// Register AgentApplicationOptions
builder.Services.AddSingleton(sp =>
{
    var storage = sp.GetService<IStorage>();
    var connections = sp.GetService<IConnections>();

    // List of OAuth Handlers (clearer than doing it inline below)
    IUserAuthorization[] handlers = [
        new AzureBotUserAuthorization(
            "auto", 
            storage, 
            connections, 
            new OAuthSettings() 
            { 
                AzureBotOAuthConnectionName = "oauth",
                Title = "SigIn for Sample",
                Text = "Please sign in and send the 6-digit code"
            }
        ),
        new AzureBotUserAuthorization(
            "me", 
            storage, 
            connections, 
            new OAuthSettings() 
            { 
                AzureBotOAuthConnectionName = "oauth2",
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
        Adapter = sp.GetService<IChannelAdapter>(),
        UserAuthorization = new(connections, handlers)
        {
            DefaultHandlerName = "auto",
            AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny,
        }
    };
});

// Add the AgentApplication
builder.AddAgent<AuthAgent>();


// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(new TokenValidationOptions()
{
    Audiences = [clientId],
    TenantId = tenantId
});

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();


app.MapGet("/", () => "Microsoft Agents SDK Sample");

// This receives incoming messages from Azure Bot Service or other SDK Agents
var route = app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (!app.Environment.IsDevelopment())
{
    route.RequireAuthorization();
}
else
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

