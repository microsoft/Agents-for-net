// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Microsoft.Agents.Samples.A2ACli.OAuth.Configuration;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Samples.A2ACli.Configuration;

internal sealed class A2ACliOptions
{
    public required Uri AgentUrl { get; init; }

    public A2ACliAuthenticationOptions Authentication { get; init; } = new();

    public A2ATransport Transport { get; init; }

    public bool ShowHistory { get; init; }

    public bool UsePushNotifications { get; init; }

    public Uri PushNotificationReceiver { get; init; } = new("http://localhost:5000");

    public static A2ACliOptions FromConfiguration(IConfiguration configuration, StartupOptions startupOptions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(startupOptions);

        Uri agentUrl = startupOptions.AgentUrl
            ?? GetRequiredAbsoluteUri(configuration, "A2A:AgentUrl");
        if (!agentUrl.IsAbsoluteUri
            || (agentUrl.Scheme != Uri.UriSchemeHttp && agentUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("The A2A Agent Card discovery URL must use HTTP or HTTPS.");
        }

        return new A2ACliOptions
        {
            AgentUrl = agentUrl,
            Transport = startupOptions.Transport ?? ParseTransport(configuration["A2A:Transport"] ?? "auto"),
            Authentication = new A2ACliAuthenticationOptions
            {
                Providers = OAuthCredentialProviderConfigurationReader.Read(
                    configuration.GetSection("Authentication:Providers")),
            },
            ShowHistory = startupOptions.ShowHistory,
            UsePushNotifications = startupOptions.UsePushNotifications,
            PushNotificationReceiver = startupOptions.PushNotificationReceiver ?? new Uri("http://localhost:5000"),
        };
    }

    internal static A2ATransport ParseTransport(string value) => value.ToLowerInvariant() switch
    {
        "auto" => A2ATransport.Auto,
        "jsonrpc" => A2ATransport.JsonRpc,
        "http-json" => A2ATransport.HttpJson,
        "grpc" => A2ATransport.Grpc,
        _ => throw new ArgumentException(
            $"Unsupported transport '{value}'. Expected auto, jsonrpc, http-json, or grpc.", nameof(value)),
    };

    private static Uri GetRequiredAbsoluteUri(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required A2A client configuration value '{key}'.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException($"A2A client configuration value '{key}' must be an absolute URI.");
        }

        return uri;
    }

}

internal sealed class StartupOptions
{
    public Uri? AgentUrl { get; init; }

    public A2AAuthMode? AuthMode { get; init; }

    public A2ATransport? Transport { get; init; }

    public bool ShowHistory { get; init; }

    public bool UsePushNotifications { get; init; }

    public Uri? PushNotificationReceiver { get; init; }

    public bool ShowHelp { get; init; }
}
