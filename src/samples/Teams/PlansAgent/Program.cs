// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PlansAgent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IStorage, MemoryStorage>();

builder.AddAgentDefaults()
    .AddAgent<PlanAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication(), forceEnable: true);

var app = builder.Build();
app.UseAgents();
app.MapDefaultAgentEndpoints();
app.Run();
