// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Core.Telemetry;

//{
//    // Setup logging to be exported via OpenTelemetry
//    builder.Logging.AddOpenTelemetry(options => options
//        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
//            serviceName: AgentsTelemetry.SourceName,
//            serviceVersion: AgentsTelemetry.SourceVersion))
//        .AddConsoleExporter());

//    var otel = builder.Services.AddOpenTelemetry()
//        .ConfigureResource(resource => resource.AddService(
//             serviceName: AgentsTelemetry.SourceName,
//             serviceVersion: AgentsTelemetry.SourceVersion))
//        .WithTracing(tracing => tracing
//            .AddSource(AgentsTelemetry.SourceName)
//            .AddAspNetCoreInstrumentation()
//            .AddConsoleExporter())
//        .WithMetrics(metrics => metrics.
//            .AddMeter(AgentsTelemetry.SourceName)
//            .AddConsoleExporter());

//    // Export OpenTelemetry data via OTLP, using env vars for the configuration
//    var OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
//    if (OtlpEndpoint != null)
//    {
//        otel.UseOtlpExporter();
//    }
//}

namespace TelemetryAgent
{
    // Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
    // This can be used by ASP.NET Core apps, Azure Functions, and other .NET apps using the Generic Host.
    // This allows you to use the local aspire desktop and monitor Agents SDK operations.
    // To learn more about using the local aspire desktop, see https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash
    public static class AgentOtelExtension
    {

        public static TBuilder ConfigureOtelProviders<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(
                        serviceName: AgentsTelemetry.SourceName,
                        serviceVersion: AgentsTelemetry.SourceVersion
                    ))
                .WithTracing(tracing => tracing
                    .AddSource(AgentsTelemetry.ActivitySource.Name)
                    .SetSampler(new AlwaysOnSampler())
                    // The rest of your setup code goes here
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter())
                .WithMetrics(metrics => metrics
                    // The rest of your setup code goes here
                    .AddRuntimeInstrumentation()
                    .AddMeter(AgentsTelemetry.SourceName)
                    .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                    {
                        metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
                    })
                    .AddOtlpExporter());

            builder.Logging.AddOpenTelemetry(options => options
                 .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                     serviceName: AgentsTelemetry.SourceName,
                     serviceVersion: AgentsTelemetry.SourceVersion))
                 .AddConsoleExporter()
                 .AddOtlpExporter());


            //public static TBuilder ConfigureOtelProviders<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
            //{
            //    builder.Logging.AddOpenTelemetry(options => options
            //        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            //            serviceName: AgentsTelemetry.SourceName,
            //            serviceVersion: AgentsTelemetry.SourceVersion))
            //        .AddOtlpExporter());

            //    builder.Services.AddOpenTelemetry()
            //        .ConfigureResource(r => r
            //        .Clear()
            //        .AddService(
            //            serviceName: AgentsTelemetry.SourceName,
            //            serviceVersion: AgentsTelemetry.SourceVersion,
            //            serviceInstanceId: Environment.MachineName)
            //        .AddAttributes(new Dictionary<string, object>
            //        {
            //            ["deployment.environment"] = builder.Environment.EnvironmentName,
            //            ["service.namespace"] = "Microsoft.Agents"
            //        }))
            //        .WithMetrics(metrics =>
            //        {
            //            metrics.AddAspNetCoreInstrumentation()
            //                .AddHttpClientInstrumentation()
            //                .AddRuntimeInstrumentation()
            //                .AddMeter(AgentsTelemetry.SourceName)
            //                .AddOtlpExporter();
            //        })
            //        .WithTracing(tracing =>
            //        {
            //            tracing.AddSource(builder.Environment.ApplicationName)
            //                .AddSource(
            //                    "Microsoft.AspNetCore",
            //                    "System.Net.Http",
            //                    AgentsTelemetry.SourceName
            //                )
            //                .AddAspNetCoreInstrumentation(tracing =>
            //                {
            //                    // Exclude health check requests from tracing
            //                    tracing.RecordException = true;
            //                    tracing.EnrichWithHttpRequest = (activity, request) =>
            //                    {
            //                        activity.SetTag("http.request.body.size", request.ContentLength);
            //                        activity.SetTag("user_agent", request.Headers.UserAgent);
            //                    };
            //                    tracing.EnrichWithHttpResponse = (activity, response) =>
            //                    {
            //                        activity.SetTag("http.response.body.size", response.ContentLength);
            //                    };
            //                })
            //                // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
            //                //.AddGrpcClientInstrumentation()
            //                .AddHttpClientInstrumentation(o =>
            //                {
            //                    o.RecordException = true;
            //                    // Enrich outgoing request/response with extra tags
            //                    o.EnrichWithHttpRequestMessage = (activity, request) =>
            //                    {
            //                        activity.SetTag("http.request.method", request.Method);
            //                        activity.SetTag("http.request.host", request.RequestUri?.Host);
            //                        activity.SetTag("http.request.useragent", request.Headers?.UserAgent);
            //                    };
            //                    o.EnrichWithHttpResponseMessage = (activity, response) =>
            //                    {
            //                        activity.SetTag("http.response.status_code", (int)response.StatusCode);
            //                        //activity.SetTag("http.response.headers", response.Content.Headers);
            //                        // Convert response.Content.Headers to a string array: "HeaderName=val1,val2"
            //                        var headerList = response.Content?.Headers?
            //                            .Select(h => $"{h.Key}={string.Join(",", h.Value)}")
            //                            .ToArray();

            //                        if (headerList is { Length: > 0 })
            //                        {
            //                            // Set as an array tag (preferred for OTEL exporters supporting array-of-primitive attributes)
            //                            activity.SetTag("http.response.headers", headerList);

            //                            // (Optional) Also emit individual header tags (comment out if too high-cardinality)
            //                            // foreach (var h in response.Content.Headers)
            //                            // {
            //                            //     activity.SetTag($"http.response.header.{h.Key.ToLowerInvariant()}", string.Join(",", h.Value));
            //                            // }
            //                        }

            //                    };
            //                })
            //                .AddOtlpExporter();
            //        });

            //var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            //if (useOtlpExporter)
            //{
            //    builder.Services.AddOpenTelemetry().UseOtlpExporter();
            //}

            // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
            //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            //{
            //    builder.Services.AddOpenTelemetry()
            //       .UseAzureMonitor();
            //}

            return builder;
        }
    }
}
