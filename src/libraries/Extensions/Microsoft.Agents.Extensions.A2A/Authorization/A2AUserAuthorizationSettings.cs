// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

/// <summary>
/// Configures an <see cref="A2AUserAuthorization"/> handler and the OAuth metadata it contributes
/// to the A2A Agent Card.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SecuritySchemeName"/> identifies the Agent Card security scheme used by the handler.
/// When <see cref="OAuthFlows"/> is configured, the handler defines that named scheme inline and
/// defaults the name to the authorization handler name when omitted.
/// Otherwise, the handler references an existing scheme with that name.
/// </para>
/// <para>
/// <see cref="RequiredScopes"/> identifies the scopes required by a generated Agent Card
/// security requirement. When omitted, it defaults to every scope advertised by the configured
/// OAuth flow. An explicit value can select a subset of those scopes. This differs from
/// <see cref="OBOSettings.OBOScopes"/>, which identifies downstream scopes requested during
/// an on-behalf-of exchange.
/// </para>
/// <para>
/// The inherited <see cref="OBOSettings.OBOConnectionName"/> selects the configured connection
/// used for an on-behalf-of exchange. When it is omitted, the handler uses the default connection
/// selected for the turn. The inherited <see cref="OBOSettings.OBOScopes"/> triggers the exchange
/// and identifies the downstream scopes to request. When no OBO scopes are configured or supplied
/// by the caller, the validated inbound A2A token is returned unchanged.
/// </para>
/// <para>
/// In <see cref="Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorizationMode.RequestToken"/> mode, the handler supplies the validated
/// inbound request token to the AgentApplication authorization pipeline. In
/// <see cref="Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorizationMode.InTask"/> mode, a <c>resumeAuth</c>
/// request supplies the delegated token through the <c>x-a2a-intask-authorization</c> header while the standard
/// HTTP <c>Authorization</c> header remains available for request authentication.
/// </para>
/// </remarks>
public sealed class A2AUserAuthorizationSettings : OBOSettings
{
    /// <summary>
    /// Gets or sets how the handler obtains its inbound credential.
    /// </summary>
    public A2AUserAuthorizationMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the Agent Card security scheme name used by this handler.
    /// </summary>
    /// <remarks>
    /// When <see cref="OAuthFlows"/> is configured, the handler defines an inline OAuth scheme
    /// with this name, defaulting to the authorization handler name when omitted. Without
    /// <see cref="OAuthFlows"/>, the handler references an existing Agent Card scheme with this
    /// name; omitting it in that case contributes no Agent Card security metadata.
    /// </remarks>
    public string SecuritySchemeName { get; set; }

    /// <summary>
    /// Gets or sets the OAuth flow used to acquire an inbound access token for the A2A agent.
    /// </summary>
    /// <remarks>
    /// Exactly one flow must be configured. For example, Device Code or Authorization Code
    /// represents delegated user authentication, while Client Credentials represents
    /// application-to-application authentication. Each flow's <c>Scopes</c> dictionary advertises
    /// the scopes that the authorization server makes available; it does not make every advertised
    /// scope required by a skill.
    /// </remarks>
    public OAuthFlows OAuthFlows { get; set; }

    /// <summary>
    /// Gets or sets the scopes that generated Agent Card security requirements request from
    /// the scheme named by <see cref="SecuritySchemeName"/>.
    /// </summary>
    /// <remarks>
    /// These values should be a subset of the scopes advertised by the resolved OAuth flow.
    /// When omitted, the SDK uses every key from the configured OAuth flow's <c>Scopes</c>
    /// dictionary, sorted ordinally. An explicit list, including an empty list, is preserved.
    /// Scope enforcement remains controlled by <see cref="EnforceRequiredScopes"/>. For Microsoft Entra Client Credentials, the acquisition
    /// scope is commonly <c>api://{resource-app-id}/.default</c>; the resulting token carries
    /// application permissions in its <c>roles</c> claim rather than its <c>scp</c> claim.
    /// </remarks>
    public IList<string> RequiredScopes { get; set; }

    /// <summary>
    /// Gets or sets whether the handler enforces <see cref="RequiredScopes"/> on the inbound token.
    /// </summary>
    /// <remarks>
    /// When enabled, the handler requires a delegated JWT with an <c>scp</c> claim containing every
    /// configured required scope. Opaque tokens and application tokens require application-specific
    /// authorization and are not supported by this built-in check. In
    /// <see cref="Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorizationMode.InTask"/>
    /// mode, configured <see cref="Microsoft.Agents.Builder.UserAuth.OBOSettings.OBOScopes"/> are
    /// also required so the client-supplied token is validated by a trusted exchange before the
    /// protected route runs. The default is <see langword="false"/>.
    /// </remarks>
    public bool EnforceRequiredScopes { get; set; }

    internal static A2AUserAuthorizationSettings FromConfiguration(IConfigurationSection configurationSection)
    {
        var settings = configurationSection?.Get<A2AUserAuthorizationSettings>() ?? new A2AUserAuthorizationSettings();
        A2AOAuthFlowConfiguration.BindScopes(configurationSection?.GetSection(nameof(OAuthFlows)), settings.OAuthFlows);
        if (settings.RequiredScopes == null
            && configurationSection?.GetSection(nameof(RequiredScopes)).Value == string.Empty)
        {
            settings.RequiredScopes = [];
        }
        ApplyDefaults(settings);

        if (settings.OBOScopes == null && configurationSection != null)
        {
            var configuredScope = configurationSection.GetSection(nameof(OBOScopes)).Get<string>();
            if (!string.IsNullOrEmpty(configuredScope))
            {
                settings.OBOScopes = [configuredScope];
            }
        }

        ValidateSecurityScheme(settings);
        ValidateRuntimeEnforcement(settings);
        return settings;
    }

    internal static void ApplyDefaults(A2AUserAuthorizationSettings settings)
    {
        if (settings?.RequiredScopes == null && settings?.OAuthFlows != null)
        {
            settings.RequiredScopes = A2AOAuthFlowConfiguration.GetScopeNames(settings.OAuthFlows);
        }
    }

    internal static A2AUserAuthorizationSettings FromOBOSettings(OBOSettings settings)
        => new()
        {
            OBOConnectionName = settings?.OBOConnectionName,
            OBOScopes = settings?.OBOScopes,
        };

    private static void ValidateSecurityScheme(A2AUserAuthorizationSettings settings)
    {
        if (settings.OAuthFlows == null)
        {
            if (settings.Mode == A2AUserAuthorizationMode.InTask)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AuthorizationExactlyOneOAuthFlowRequired,
                    null);
            }
            return;
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
    }

    internal static void ValidateRuntimeEnforcement(A2AUserAuthorizationSettings settings)
    {
        if (settings.EnforceRequiredScopes
            && (settings.RequiredScopes == null
                || settings.RequiredScopes.Count == 0
                || settings.RequiredScopes.Any(string.IsNullOrWhiteSpace)))
        {
            throw ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AuthorizationRequiredScopesMissing,
                null);
        }

        if (settings.Mode == A2AUserAuthorizationMode.InTask
            && settings.EnforceRequiredScopes
            && (settings.OBOScopes == null || settings.OBOScopes.Count == 0))
        {
            throw ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AuthorizationInTaskScopeEnforcementRequiresOBO,
                null);
        }
    }
}
