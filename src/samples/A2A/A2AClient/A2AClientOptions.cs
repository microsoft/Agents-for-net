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
                Providers = ReadOAuthProviders(configuration.GetSection("Authentication:Providers")),
            },
            ShowHistory = startupOptions.ShowHistory,
            UsePushNotifications = startupOptions.UsePushNotifications,
            PushNotificationReceiver = startupOptions.PushNotificationReceiver ?? new Uri("http://localhost:5000"),
        };
    }

    private static IReadOnlyDictionary<string, OAuthCredentialProviderOptions> ReadOAuthProviders(
        IConfigurationSection providersSection)
    {
        var providers = new Dictionary<string, OAuthCredentialProviderOptions>(StringComparer.Ordinal);
        foreach (IConfigurationSection providerSection in providersSection.GetChildren())
        {
            string providerId = providerSection.Key;
            OAuthCredentialProviderType type = ParseEnumValue<OAuthCredentialProviderType>(providerSection, "Type");

            Uri[] allowedAuthorities = ReadTrustedUris(providerSection.GetSection("AllowedAuthorities"));
            if (type == OAuthCredentialProviderType.Entra && allowedAuthorities.Length == 0)
            {
                allowedAuthorities = [new Uri("https://login.microsoftonline.com")];
            }

            Uri[] allowedOrigins = ReadTrustedUris(providerSection.GetSection("AllowedOrigins"));
            bool allowInteractiveApproval = GetOptionalBoolean(providerSection["AllowInteractiveApproval"]);
            Uri? redirectUri = GetOptionalRedirectUri(providerSection, "RedirectUri");
            if (type == OAuthCredentialProviderType.OAuth21PkceDcr
                && allowInteractiveApproval
                && redirectUri is null)
            {
                throw new InvalidOperationException(
                    $"A2A client configuration value '{providerSection.Path}:RedirectUri' is required when interactive approval is enabled.");
            }

            providers.Add(
                providerId,
                new OAuthCredentialProviderOptions
                {
                    Id = providerId,
                    Type = type,
                    AllowedAuthorities = allowedAuthorities,
                    AllowedOrigins = allowedOrigins,
                    AdditionalScopes = ReadAdditionalScopes(providerSection.GetSection("AdditionalScopes")),
                    Registrations = ReadRegistrations(providerSection.GetSection("Registrations"), providerId, type),
                    AllowInteractiveApproval = allowInteractiveApproval,
                    RedirectUri = redirectUri,
                    MetadataUrl = GetOptionalTrustedUri(providerSection, "MetadataUrl"),
                    ServerUrl = GetOptionalTrustedUri(providerSection, "ServerUrl"),
                });
        }

        return providers;
    }

    private static IReadOnlyDictionary<string, OAuthClientRegistration> ReadRegistrations(
        IConfigurationSection registrationsSection,
        string providerId,
        OAuthCredentialProviderType providerType)
    {
        var registrations = new Dictionary<string, OAuthClientRegistration>(StringComparer.Ordinal);
        foreach (IConfigurationSection registrationSection in registrationsSection.GetChildren())
        {
            string registrationId = registrationSection.Key;
            IReadOnlyList<A2AOAuthFlowType> grantTypes = ReadGrantTypes(
                registrationSection.GetSection("GrantTypes"),
                registrationSection.Path + ":GrantTypes");
            string clientId = registrationSection["ClientId"]
                ?? throw new InvalidOperationException(
                    $"Missing required A2A client configuration value '{registrationSection.Path}:ClientId'.");
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException(
                    $"A2A client configuration value '{registrationSection.Path}:ClientId' must not be blank.");
            }

            bool usePkce = GetOptionalBoolean(registrationSection["UsePkce"], defaultValue: true);
            if (providerType is OAuthCredentialProviderType.GenericOAuth2Pkce or OAuthCredentialProviderType.OAuth21PkceDcr)
            {
                usePkce = true;
            }

            registrations.Add(
                registrationId,
                new OAuthClientRegistration(
                    registrationId,
                    grantTypes,
                    clientId,
                    registrationSection["ClientSecret"],
                    GetOptionalRedirectUri(registrationSection, "RedirectUri"),
                    ParseOptionalEnumValue(
                        registrationSection,
                        "TokenEndpointAuthenticationMethod",
                        defaultValue: OAuthTokenEndpointAuthenticationMethod.None),
                    usePkce));
        }

        return registrations;
    }

    private static IReadOnlyList<A2AOAuthFlowType> ReadGrantTypes(
        IConfigurationSection grantTypesSection,
        string key)
    {
        var grantTypes = new List<A2AOAuthFlowType>();
        var seenGrantTypes = new HashSet<A2AOAuthFlowType>();
        foreach (IConfigurationSection grantTypeSection in grantTypesSection.GetChildren())
        {
            A2AOAuthFlowType grantType = ParseEnumValue<A2AOAuthFlowType>(grantTypeSection);
            if (!seenGrantTypes.Add(grantType))
            {
                throw new InvalidOperationException(
                    $"A2A client configuration value '{key}' contains duplicate grant type '{grantType}'.");
            }

            grantTypes.Add(grantType);
        }

        return grantTypes;
    }

    private static Uri[] ReadTrustedUris(IConfigurationSection section)
        => section
            .GetChildren()
            .Select(item => GetRequiredTrustedUri(item.Value, item.Path))
            .ToArray();

    private static IReadOnlyList<string> ReadAdditionalScopes(IConfigurationSection additionalScopesSection)
        => additionalScopesSection
            .GetChildren()
            .Select(scope => scope.Value)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static TEnum ParseEnumValue<TEnum>(IConfigurationSection configuration)
        where TEnum : struct, Enum
        => ParseEnumValue<TEnum>(configuration, key: null);

    private static TEnum ParseEnumValue<TEnum>(IConfigurationSection configuration, string? key)
        where TEnum : struct, Enum
    {
        string configurationKey = key is null ? configuration.Path : $"{configuration.Path}:{key}";
        string? value = key is null ? configuration.Value : configuration[key];
        if (string.IsNullOrWhiteSpace(value)
            || !Enum.TryParse(value, ignoreCase: true, out TEnum parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException(
                $"A2A client configuration value '{configurationKey}' must be a supported {typeof(TEnum).Name} value.");
        }

        return parsed;
    }

    private static TEnum ParseOptionalEnumValue<TEnum>(IConfigurationSection configuration, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        string? value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : ParseEnumValue<TEnum>(configuration, key);
    }

    private static bool GetOptionalBoolean(string? value, bool defaultValue = false)
        => bool.TryParse(value, out bool parsed) ? parsed : defaultValue;

    private static Uri? GetOptionalTrustedUri(IConfigurationSection configuration, string key)
    {
        string? value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : GetRequiredTrustedUri(configuration, key);
    }

    private static Uri? GetOptionalRedirectUri(IConfigurationSection configuration, string key)
    {
        string? value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : GetRequiredRedirectUri(configuration, key);
    }

    private static Uri GetRequiredTrustedUri(IConfigurationSection configuration, string key)
        => ValidateTrustedUri(GetRequiredAbsoluteUri(configuration, key), GetConfigurationKey(configuration, key));

    private static Uri GetRequiredTrustedUri(string? value, string key)
        => ValidateTrustedUri(GetRequiredAbsoluteUri(value, key), key);

    private static Uri GetRequiredRedirectUri(IConfigurationSection configuration, string key)
        => ValidateRedirectUri(GetRequiredAbsoluteUri(configuration, key), GetConfigurationKey(configuration, key));

    private static Uri ValidateTrustedUri(Uri uri, string key)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"A2A client configuration value '{key}' must use HTTPS.");
        }

        return uri;
    }

    private static Uri ValidateRedirectUri(Uri uri, string key)
    {
        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback)
        {
            return uri;
        }

        throw new InvalidOperationException(
            $"A2A client configuration value '{key}' must use HTTPS or loopback HTTP.");
    }

    private static string GetConfigurationKey(IConfigurationSection configuration, string key)
        => $"{configuration.Path}:{key}";

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
