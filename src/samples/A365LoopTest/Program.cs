// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A365LoopTest;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgentMiddleware(new TranscriptLoggerMiddleware(new FileTranscriptLogger()))
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.UseAgents()
    .MapDefaultAgentEndpoints();

app.Run();
