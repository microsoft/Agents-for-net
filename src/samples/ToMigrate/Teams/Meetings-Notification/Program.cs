// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using InMeetingNotificationsBot.Bots;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMvc();
builder.Services.AddRazorPages();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add basic bot functionality
builder.AddBot<InMeetingNotifications>();

var app = builder.Build();
app.MapRazorPages();
app.MapControllerRoute(
   name: "default",
   pattern: "{controller=Home}/{action=CustomForm}/{id?}");

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

app.MapControllerRoute(
   name: "default",
   pattern: "{controller=Home}/{action=CustomForm}/{id?}");

app.Run();
