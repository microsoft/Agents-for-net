// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AuthenticationBot;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.UserAuth.TokenService;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions with User Authentication handlers.
builder.Services.AddTransient(sp =>
{
    var adapter = sp.GetService<IChannelAdapter>();
    var storage = sp.GetService<IStorage>();

    var authOptions = new UserAuthenticationOptions()
    {
        // Auto-SignIn will use this OAuth flow
        Default = "graph",

        AutoSignIn = (context, cancellationToken) =>
        {
            return Task.FromResult(context.Activity.Text == "auto");
        },

        Handlers =
        [
            new OAuthAuthentication(
                "graph",
                new OAuthSettings()
                {
                    ConnectionName = builder.Configuration["ConnectionName"]
                },
                storage)]
    };

    return new AgentApplicationOptions(builder.Configuration, adapter, storage)
    {
        UserAuthentication = authOptions
    };
});

// Add the bot (which is transient)
builder.AddBot<AuthBot>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();

