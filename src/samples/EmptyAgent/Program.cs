// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EmptyAgent;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

app.UseAgents()
    .MapDefaultAgentEndpoints();

app.Run();
