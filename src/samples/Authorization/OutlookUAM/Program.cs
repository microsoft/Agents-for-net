// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OutlookUAM;
using Microsoft.Agents.Hosting.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<UAMAgent>()
    .AddAgentM365AttachmentDownloader()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

app.UseAgents();

// Map agent endpoints for "/" and "/api/messages".
app.MapDefaultAgentEndpoints();

app.Run();
