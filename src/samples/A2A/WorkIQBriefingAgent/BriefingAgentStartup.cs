// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using System.Net.Http;

namespace WorkIQBriefingAgent;

internal static class BriefingAgentStartup
{
    public static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.ConfigureOtelProviders();
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource("A2A", "A2A.AspNetCore", "WorkIQBriefingAgent"))
            .WithMetrics(metrics => metrics.AddMeter("A2A"));
        builder.AddAgentDefaults()
            .AddAgent<BriefingAgent>()
            // Never disable validation in Development: this sample reads work data and sends real mail.
            .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication(), forceEnable: true);

        // This is a local, single-process sample. MemoryStorage has optimistic concurrency, but no restart recovery.
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ITaskStore, UserTaskStore>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["WorkIQ:TimeZone"] ?? "UTC"));
        builder.Services.AddSingleton<IMailSender, GraphMailSender>();
        builder.Services.AddSingleton<BriefingWorkflow>();
        builder.Services.AddSingleton<WorkIqClient>();
        builder.Services.AddHttpClient(WorkIqClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://workiq.svc.cloud.microsoft/a2a/");
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddHttpClient(GraphMailSender.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    }

    public static void ConfigureApplication(WebApplication app)
    {
        app.UseAgents();
        app.MapGet("/", () => "Work IQ briefing sample. Discover /.well-known/agent-card.json and connect with A2ACli.");
        app.MapA2AApplicationEndpoints(requireAuth: true);
    }
}
