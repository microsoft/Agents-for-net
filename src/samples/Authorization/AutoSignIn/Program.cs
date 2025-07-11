// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

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
                    Authority = "https://login.microsoftonline.com/6d49904d-ce08-4e4d-bcf0-26c4142fc209",
                    ClientId = "{{redacted-but-use-env-anyway}}",
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

// Register IChannelServiceClientFactory
builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

// Using extension for CloudAdapter (because of internal HostedServices bits)
// Custom Adapters would do it directly: `builder.Services.AddSingleton<IChannelAdapter>(blahblah)`
// CloudAdapter subclasses would use `AddCloudAdapter<MySpecialCloudAdapter>()`
builder.Services.AddCloudAdapter();

// Add the AgentApplication
builder.Services.AddTransient<IAgent, AuthAgent>();

// And just for fun register a compat TranscriptLogger (because I haven't submitted the AgentApplication friendly version)
builder.Services.AddSingleton<Microsoft.Agents.Builder.IMiddleware[]>([
    new TranscriptLoggerMiddleware(new FileTranscriptLogger())
]);


// Configure the HTTP request pipeline.

WebApplication app = builder.Build();

app.MapGet("/", () => "Microsoft Agents SDK Sample");

// This receives incoming messages from Azure Bot Service or other SDK Agents
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

