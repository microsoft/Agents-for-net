// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

internal static class OAuthCredentialProviderConfigurationReader
{
    public static IReadOnlyDictionary<string, OAuthCredentialProviderOptions> Read(
        IConfigurationSection providersSection)
    {
        ArgumentNullException.ThrowIfNull(providersSection);
        return ReadOAuthProviders(providersSection);
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
            bool allowInteractiveApproval = ParseOptionalBoolean(
                providerSection,
                "AllowInteractiveApproval",
                defaultValue: false);
            Uri? redirectUri = GetOptionalRedirectUri(providerSection, "RedirectUri");
            if (type == OAuthCredentialProviderType.OAuth21PkceDcr && redirectUri is null)
            {
                throw new InvalidOperationException(
                    $"Missing required A2A client configuration value '{providerSection.Path}:RedirectUri' "
                    + $"for provider type '{OAuthCredentialProviderType.OAuth21PkceDcr}'.");
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

            bool usePkce = ParseOptionalBoolean(registrationSection, "UsePkce", defaultValue: true);
            if (providerType is OAuthCredentialProviderType.GenericOAuth2Pkce or OAuthCredentialProviderType.OAuth21PkceDcr)
            {
                usePkce = true;
            }

            string? clientSecret = registrationSection["ClientSecret"];
            OAuthTokenEndpointAuthenticationMethod authenticationMethod = ParseOptionalEnumValue(
                registrationSection,
                "TokenEndpointAuthenticationMethod",
                defaultValue: OAuthTokenEndpointAuthenticationMethod.None);
            ValidateRegistrationClientAuthentication(
                registrationSection,
                grantTypes,
                clientSecret,
                authenticationMethod);

            registrations.Add(
                registrationId,
                new OAuthClientRegistration(
                    registrationId,
                    grantTypes,
                    clientId,
                    clientSecret,
                    GetOptionalRedirectUri(registrationSection, "RedirectUri"),
                    authenticationMethod,
                    usePkce));
        }

        return registrations;
    }

    /// <summary>
    /// Rejects client-authentication settings that cannot produce a usable token request.
    /// </summary>
    /// <remarks>
    /// The resolver also skips incompatible registrations, but a contradictory registration is a configuration
    /// mistake rather than a deliberate non-match, so it fails at startup where the offending key can be named.
    /// </remarks>
    private static void ValidateRegistrationClientAuthentication(
        IConfigurationSection registrationSection,
        IReadOnlyList<A2AOAuthFlowType> grantTypes,
        string? clientSecret,
        OAuthTokenEndpointAuthenticationMethod authenticationMethod)
    {
        bool hasClientSecret = !string.IsNullOrWhiteSpace(clientSecret);
        if (authenticationMethod == OAuthTokenEndpointAuthenticationMethod.None)
        {
            if (hasClientSecret)
            {
                throw new InvalidOperationException(
                    $"A2A client configuration value '{registrationSection.Path}:ClientSecret' must be empty when "
                    + $"'TokenEndpointAuthenticationMethod' is '{OAuthTokenEndpointAuthenticationMethod.None}'.");
            }

            if (grantTypes.Contains(A2AOAuthFlowType.ClientCredentials))
            {
                throw new InvalidOperationException(
                    $"A2A client configuration value '{registrationSection.Path}:TokenEndpointAuthenticationMethod' "
                    + $"must authenticate the client for grant type '{A2AOAuthFlowType.ClientCredentials}'.");
            }

            return;
        }

        if (!hasClientSecret)
        {
            throw new InvalidOperationException(
                $"Missing required A2A client configuration value '{registrationSection.Path}:ClientSecret' "
                + $"for 'TokenEndpointAuthenticationMethod' '{authenticationMethod}'.");
        }
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

        if (grantTypes.Count == 0)
        {
            throw new InvalidOperationException(
                $"A2A client configuration value '{key}' must contain at least one grant type.");
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

    private static bool ParseOptionalBoolean(IConfigurationSection configuration, string key, bool defaultValue)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!bool.TryParse(value.Trim(), out bool parsed))
        {
            throw new InvalidOperationException(
                $"A2A client configuration value '{GetConfigurationKey(configuration, key)}' must be 'true' or 'false'.");
        }

        return parsed;
    }

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
        => GetRequiredTrustedUri(configuration[key], GetConfigurationKey(configuration, key));

    private static Uri GetRequiredTrustedUri(string? value, string key)
        => ValidateTrustedUri(GetRequiredAbsoluteUri(value, key), key);

    private static Uri GetRequiredRedirectUri(IConfigurationSection configuration, string key)
        => ValidateRedirectUri(
            GetRequiredAbsoluteUri(configuration[key], GetConfigurationKey(configuration, key)),
            GetConfigurationKey(configuration, key));

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
        if (!OAuthEndpointValidator.IsSupportedLoopbackRedirectUri(uri))
        {
            throw new InvalidOperationException(
                $"A2A client configuration value '{key}' must be an absolute loopback HTTP URI whose path ends with '/'.");
        }

        return uri;
    }

    private static string GetConfigurationKey(IConfigurationSection configuration, string key)
        => $"{configuration.Path}:{key}";

    private static Uri GetRequiredAbsoluteUri(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException($"A2A client configuration value '{key}' must be an absolute URI.");
        }

        return uri;
    }
}
