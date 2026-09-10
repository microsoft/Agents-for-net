// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AClientOptions
{
    public required Uri AgentUrl { get; init; }

    public A2AClientAuthenticationOptions Authentication { get; init; } = new();

    public bool ShowHistory { get; init; }

    public bool UsePushNotifications { get; init; }

    public Uri PushNotificationReceiver { get; init; } = new("http://localhost:5000");

    public static A2AClientOptions FromConfiguration(IConfiguration configuration, StartupOptions startupOptions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(startupOptions);

        Uri agentUrl = startupOptions.AgentUrl
            ?? GetRequiredAbsoluteUri(configuration, "A2A:AgentUrl");

        return new A2AClientOptions
        {
            AgentUrl = agentUrl,
            Authentication = new A2AClientAuthenticationOptions
            {
                TenantId = configuration["Authentication:TenantId"],
                PublicClientId = configuration["Authentication:PublicClientId"],
                ConfidentialClientId = configuration["Authentication:ConfidentialClientId"],
                ConfidentialClientSecret = configuration["Authentication:ConfidentialClientSecret"],
                AgentDelegatedScope = configuration["Authentication:AgentDelegatedScope"],
                AgentApplicationScope = configuration["Authentication:AgentApplicationScope"],
            },
            ShowHistory = startupOptions.ShowHistory,
            UsePushNotifications = startupOptions.UsePushNotifications,
            PushNotificationReceiver = startupOptions.PushNotificationReceiver ?? new Uri("http://localhost:5000"),
        };
    }

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

    public A2AAuthMode AuthMode { get; init; } = A2AAuthMode.None;

    public bool ShowHistory { get; init; }

    public bool UsePushNotifications { get; init; }

    public Uri? PushNotificationReceiver { get; init; }

    public bool ShowHelp { get; init; }
}
