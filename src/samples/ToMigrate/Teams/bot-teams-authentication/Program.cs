// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TeamsAuth.Bots;
using TeamsAuth.Dialogs;
using Microsoft.Agents.BotBuilder.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add basic bot functionality
builder.AddBot<TeamsBot<MainDialog>>();

// Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// For SSO, use CosmosDbPartitionedStorage

/* COSMOSDB STORAGE - Uncomment the code in this section to use CosmosDB storage */

// var cosmosDbStorageOptions = new CosmosDbPartitionedStorageOptions()
// {
//     CosmosDbEndpoint = "<endpoint-for-your-cosmosdb-instance>",
//     AuthKey = "<your-cosmosdb-auth-key>",
//     DatabaseId = "<your-database-id>",
//     ContainerId = "<cosmosdb-container-id>"
// };
// var storage = new CosmosDbPartitionedStorage(cosmosDbStorageOptions);

/* END COSMOSDB STORAGE */

// Create the User state. (Used in this bot's Dialog implementation.)
builder.Services.AddTransient<UserState>();

// Create the Conversation state. (Used by the Dialog system itself.)
builder.Services.AddTransient<ConversationState>();

// The Dialog that will be run by the bot.
builder.Services.AddSingleton<MainDialog>();

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
