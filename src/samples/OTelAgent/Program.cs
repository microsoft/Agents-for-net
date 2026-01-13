// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Azure Monitor exporter
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
// Configuration extensions
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
// OpenTelemetry imports
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OTelAgent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Application Insights connection string (appsettings or env)
string? aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] 
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

// Optional toggle via config/env to switch HttpClient instrumentation on/off
bool enableHttpClient = builder.Configuration.GetValue("OpenTelemetry:EnableHttpClient", true);

// OpenTelemetry: Agents + ASP.NET Core + System.Net (HTTP) with HttpClient tracing + metrics
var otelBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .Clear()
        .AddService(
            serviceName: "OTelAgent",
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.namespace"] = "Microsoft.Agents"
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(
                "OTelAgent",
                "Microsoft.Agents.Builder",
                "Microsoft.Agents.Hosting",
                "OTelAgent.MyAgent",
                "Microsoft.AspNetCore",
                "System.Net.Http") // HttpClient ActivitySource
            .AddAspNetCoreInstrumentation(o =>
            {
                o.RecordException = true;
                o.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("http.request.body.size", request.ContentLength);
                    activity.SetTag("user_agent", request.Headers.UserAgent);
                };
                o.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response.body.size", response.ContentLength);
                };
            });

        if (enableHttpClient)
        {
            tracing.AddHttpClientInstrumentation(o =>
            {
                o.RecordException = true;
                // Enrich outgoing request/response with extra tags
                o.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    activity.SetTag("http.request.method", request.Method);
                    activity.SetTag("http.request.host", request.RequestUri?.Host);
                };
                o.EnrichWithHttpResponseMessage = (activity, response) =>
                {
                    activity.SetTag("http.response.status_code", (int)response.StatusCode);
                };
                // Example filter: suppress telemetry for health checks
                o.FilterHttpRequestMessage = request =>
                    !request.RequestUri?.AbsolutePath.Contains("health", StringComparison.OrdinalIgnoreCase) ?? true;
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(
                "OTelAgent",
                "Microsoft.Agents",
                "System.Net.Http") // HttpClient metrics
            .AddAspNetCoreInstrumentation();

    });

// Attach Azure Monitor exporter only if a connection string is present.
if (!string.IsNullOrWhiteSpace(aiConnectionString))
{
    otelBuilder.UseAzureMonitorExporter(o =>
    {
        o.ConnectionString = aiConnectionString;
    });
}

// Logging -> forward structured logs to Application Insights (optional; remove if not needed)
builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
    o.ParseStateValues = true;
});

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication logic
builder.AddAgent<MyAgent>();

// In-memory storage (development)
builder.Services.AddSingleton<IStorage, MemoryStorage>();

builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

app.MapAgentEndpoints(requireAuth: !app.Environment.IsDevelopment(), process: async (request, response, adapter, agent, cancellationToken) =>
{
    using var activity = AgentTelemetry.ActivitySource.StartActivity("agent.process_message");

    try
    {
        activity?.SetTag("agent.type", agent.GetType().Name);
        activity?.SetTag("request.path", request.Path);
        activity?.SetTag("request.method", request.Method);

        await adapter.ProcessAsync(request, response, agent, cancellationToken);

        activity?.SetStatus(ActivityStatusCode.Ok);
        AgentTelemetry.MessageProcessedCounter.Add(1,
            new KeyValuePair<string, object?>("agent.type", agent.GetType().Name),
            new KeyValuePair<string, object?>("status", "success"));
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
        {
            ["exception.type"] = ex.GetType().FullName,
            ["exception.message"] = ex.Message,
            ["exception.stacktrace"] = ex.StackTrace
        }));
        AgentTelemetry.MessageProcessedCounter.Add(1,
            new KeyValuePair<string, object?>("agent.type", agent.GetType().Name),
            new KeyValuePair<string, object?>("status", "error"));
        throw;
    }
});

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

// Static class for Agent-specific telemetry
public static class AgentTelemetry
{
    public static readonly ActivitySource ActivitySource = new("OTelAgent");

    private static readonly Meter Meter = new("OTelAgent");

    public static readonly Counter<long> MessageProcessedCounter = Meter.CreateCounter<long>(
        "agent.messages.processed",
        "messages",
        "Number of messages processed by the agent");

    public static readonly Counter<long> RouteExecutedCounter = Meter.CreateCounter<long>(
        "agent.routes.executed",
        "routes",
        "Number of routes executed by the agent");

    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
        "agent.message.processing.duration",
        "ms",
        "Duration of message processing in milliseconds");

    public static readonly Histogram<double> RouteExecutionDuration = Meter.CreateHistogram<double>(
        "agent.route.execution.duration",
        "ms",
        "Duration of route execution in milliseconds");

    public static readonly UpDownCounter<long> ActiveConversations = Meter.CreateUpDownCounter<long>(
        "agent.conversations.active",
        "conversations",
        "Number of active conversations");
}