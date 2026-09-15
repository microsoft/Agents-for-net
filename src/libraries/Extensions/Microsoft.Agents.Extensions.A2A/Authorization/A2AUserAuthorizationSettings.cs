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
/// Configures an <see cref="A2AUserAuthorization"/> handler and the OAuth metadata it contributes
/// to the A2A Agent Card.
/// </summary>
/// <remarks>
/// <para>
/// Agent Card security metadata requires either <see cref="SecurityScheme"/> to reference an
/// existing scheme, or <see cref="SecuritySchemeName"/> together with <see cref="OAuthFlows"/>
/// to define an inline scheme. These alternatives cannot be combined.
/// </para>
/// <para>
/// <see cref="RequiredScopes"/> identifies the scopes required by a generated Agent Card
/// security requirement. This differs from the scope dictionary on an OAuth flow, which
/// advertises all scopes available from that authorization server. It also differs from
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
/// The handler supplies the validated inbound request token to the AgentApplication authorization
/// pipeline. In this preview, <see cref="AuthorizationPolicy"/> and <see cref="RequiredScopes"/>
/// contribute metadata but are not automatically evaluated as runtime authorization rules.
/// Applications must enforce their required claims, scopes, roles, or ASP.NET Core policies.
/// </para>
/// </remarks>
public sealed class A2AUserAuthorizationSettings : OBOSettings
{
    /// <summary>
    /// Gets or sets the name of an existing security scheme on the Agent Card.
    /// </summary>
    /// <remarks>
    /// Use this property when multiple handlers or skills share a centrally configured scheme.
    /// Do not set it together with <see cref="OAuthFlows"/>. When neither this property nor
    /// <see cref="OAuthFlows"/> is configured, the handler contributes no Agent Card security
    /// metadata and cannot be used to generate a protected skill requirement.
    /// </remarks>
    public string SecurityScheme { get; set; }

    /// <summary>
    /// Gets or sets the Agent Card name assigned to the inline scheme defined by
    /// <see cref="OAuthFlows"/>.
    /// </summary>
    /// <remarks>
    /// This value is required when <see cref="OAuthFlows"/> is configured and is ignored when
    /// the handler references an existing <see cref="SecurityScheme"/>.
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
    /// <see cref="SecurityScheme"/> or the inline scheme named by <see cref="SecuritySchemeName"/>.
    /// </summary>
    /// <remarks>
    /// These values should be a subset of the scopes advertised by the resolved OAuth flow.
    /// When omitted, generated requirements contain an empty scope list. The SDK does not infer
    /// required scopes from the flow's available-scope catalog and does not automatically enforce
    /// the scopes in the inbound token. For Microsoft Entra Client Credentials, the acquisition
    /// scope is commonly <c>api://{resource-app-id}/.default</c>; the resulting token carries
    /// application permissions in its <c>roles</c> claim rather than its <c>scp</c> claim.
    /// </remarks>
    public IList<string> RequiredScopes { get; set; }

    /// <summary>
    /// Gets or sets the optional name of an ASP.NET Core authorization policy associated with
    /// this handler.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. This preview records the value as handler metadata
    /// but does not evaluate the named policy. Omitting it has no effect on token acquisition or
    /// OBO exchange; applications must currently apply and evaluate authorization policies in
    /// their host or route code.
    /// </remarks>
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
