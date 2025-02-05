// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Bot1;
using Bot1.Controllers;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

builder.AddBot<HostBot>();
builder.AddChannelHost<StubResponseHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample - BotToBot Sample - Bot1");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();