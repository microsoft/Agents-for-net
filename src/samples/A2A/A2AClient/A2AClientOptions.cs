// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
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
                Connections = ReadOAuthConnections(configuration.GetSection("Authentication:Connections")),
            },
            ShowHistory = startupOptions.ShowHistory,
            UsePushNotifications = startupOptions.UsePushNotifications,
            PushNotificationReceiver = startupOptions.PushNotificationReceiver ?? new Uri("http://localhost:5000"),
        };
    }

    private static IReadOnlyDictionary<string, OAuthConnectionOptions> ReadOAuthConnections(
        IConfigurationSection connectionsSection)
    {
        var connections = new Dictionary<string, OAuthConnectionOptions>(StringComparer.Ordinal);
        foreach (IConfigurationSection connectionSection in connectionsSection.GetChildren())
        {
            string? redirectUriValue = connectionSection["RedirectUri"];
            Uri? redirectUri = string.IsNullOrWhiteSpace(redirectUriValue)
                ? null
                : GetRequiredAbsoluteUri(connectionSection, "RedirectUri");
            OAuthTokenEndpointAuthenticationMethod authenticationMethod =
                Enum.TryParse(
                    connectionSection["TokenEndpointAuthenticationMethod"],
                    ignoreCase: true,
                    out OAuthTokenEndpointAuthenticationMethod configuredMethod)
                    ? configuredMethod
                    : OAuthTokenEndpointAuthenticationMethod.None;

            connections.Add(
                connectionSection.Key,
                new OAuthConnectionOptions
                {
                    ClientId = connectionSection["ClientId"],
                    ClientSecret = connectionSection["ClientSecret"],
                    RedirectUri = redirectUri,
                    AllowedOrigins = connectionSection
                        .GetSection("AllowedOrigins")
                        .GetChildren()
                        .Select(origin => GetRequiredAbsoluteUri(origin.Value, origin.Path))
                        .ToArray(),
                    AdditionalScopes = connectionSection
                        .GetSection("AdditionalScopes")
                        .GetChildren()
                        .Select(scope => scope.Value)
                        .Where(scope => !string.IsNullOrWhiteSpace(scope))
                        .Select(scope => scope!)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    TokenEndpointAuthenticationMethod = authenticationMethod,
                    UsePkce = !bool.TryParse(connectionSection["UsePkce"], out bool usePkce) || usePkce,
                });
        }

        return connections;
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

    private static Uri GetRequiredAbsoluteUri(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
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

    public bool ShowHistory { get; init; }

    public bool UsePushNotifications { get; init; }

    public Uri? PushNotificationReceiver { get; init; }

    public bool ShowHelp { get; init; }
}
