// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2AAgent;
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

A2AAgentStartup.ConfigureBuilder(builder);

WebApplication app = builder.Build();

A2AAgentStartup.ConfigureApplication(app);

app.Run();
