// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using ProtocolExtensionAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ProtocolExtensionAgentStartup.ConfigureBuilder(builder);

WebApplication app = builder.Build();

ProtocolExtensionAgentStartup.ConfigureApplication(app);

app.Run();
