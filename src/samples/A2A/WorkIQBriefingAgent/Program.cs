// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using WorkIQBriefingAgent;

var builder = WebApplication.CreateBuilder(args);
BriefingAgentStartup.ConfigureBuilder(builder);
var app = builder.Build();
BriefingAgentStartup.ConfigureApplication(app);
app.Run();
