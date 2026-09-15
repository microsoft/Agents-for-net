// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

/// <summary>
/// Settings for an <see cref="A2AUserAuthorization"/> handler.
/// </summary>
/// <remarks>
/// Agent Card security metadata requires either <see cref="SecurityScheme"/> or
/// <see cref="SecuritySchemeName"/> together with <see cref="OAuthFlows"/>.
/// </remarks>
public sealed class A2AUserAuthorizationSettings : OBOSettings
{
    /// <summary>
    /// Gets or sets the name of a security scheme defined on the Agent Card.
    /// </summary>
    public string SecurityScheme { get; set; }

    /// <summary>
    /// Gets or sets the name assigned to an inline OAuth security scheme.
    /// </summary>
    public string SecuritySchemeName { get; set; }

    /// <summary>
    /// Gets or sets the OAuth flows for an inline security scheme.
    /// </summary>
    public OAuthFlows OAuthFlows { get; set; }

    /// <summary>
    /// Gets or sets the scopes required by the Agent Card security scheme.
    /// </summary>
    public IList<string> RequiredScopes { get; set; }

    /// <summary>
    /// Gets or sets the authorization policy associated with this handler.
    /// </summary>
    public string AuthorizationPolicy { get; set; }

    internal static A2AUserAuthorizationSettings FromConfiguration(IConfigurationSection configurationSection)
    {
        var settings = configurationSection?.Get<A2AUserAuthorizationSettings>() ?? new A2AUserAuthorizationSettings();
        A2AOAuthFlowConfiguration.BindScopes(configurationSection?.GetSection(nameof(OAuthFlows)), settings.OAuthFlows);

        if (settings.OBOScopes == null && configurationSection != null)
        {
            var configuredScope = configurationSection.GetSection(nameof(OBOScopes)).Get<string>();
            if (!string.IsNullOrEmpty(configuredScope))
            {
                settings.OBOScopes = [configuredScope];
            }
        }

        ValidateSecurityScheme(settings);
        return settings;
    }

    internal static A2AUserAuthorizationSettings FromOBOSettings(OBOSettings settings)
        => new()
        {
            OBOConnectionName = settings?.OBOConnectionName,
            OBOScopes = settings?.OBOScopes,
        };

    private static void ValidateSecurityScheme(A2AUserAuthorizationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SecurityScheme))
        {
            if (settings.OAuthFlows == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.SecuritySchemeName))
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AuthorizationSecuritySchemeNameRequired,
                    null);
            }

            var flowCount = 0;
            flowCount += settings.OAuthFlows.AuthorizationCode == null ? 0 : 1;
            flowCount += settings.OAuthFlows.ClientCredentials == null ? 0 : 1;
            flowCount += settings.OAuthFlows.DeviceCode == null ? 0 : 1;
#pragma warning disable CS0618 // Deprecated flows must still be rejected when present in configuration.
            flowCount += settings.OAuthFlows.Implicit == null ? 0 : 1;
            flowCount += settings.OAuthFlows.Password == null ? 0 : 1;
#pragma warning restore CS0618

            if (flowCount != 1)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AuthorizationExactlyOneOAuthFlowRequired,
                    null);
            }

            return;
        }

        if (settings.OAuthFlows != null)
        {
            throw ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AuthorizationSecuritySchemeConflict,
                null);
        }
    }
}
