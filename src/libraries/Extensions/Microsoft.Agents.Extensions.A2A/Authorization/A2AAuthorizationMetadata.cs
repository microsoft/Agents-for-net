// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

/// <summary>
/// Normalized authorization metadata for a configured <see cref="A2AUserAuthorization"/> handler.
/// </summary>
internal sealed class A2AAuthorizationMetadata
{
    private A2AAuthorizationMetadata(string handlerName, A2AUserAuthorizationSettings settings)
    {
        HandlerName = handlerName;
        SecuritySchemeName = string.IsNullOrWhiteSpace(settings.SecuritySchemeName)
            ? null
            : settings.SecuritySchemeName;
        ReferencedSecurityScheme = settings.OAuthFlows == null
            ? SecuritySchemeName
            : null;
        SecurityScheme = settings.OAuthFlows == null
            ? null
            : new SecurityScheme
            {
                OAuth2SecurityScheme = new OAuth2SecurityScheme
                {
                    Flows = settings.OAuthFlows,
                },
            };
        RequiredScopes = settings.RequiredScopes;
        AuthorizationPolicy = settings.AuthorizationPolicy;
        OBOSettings = settings;
    }

    /// <summary>
    /// Gets the configured authorization handler name.
    /// </summary>
    public string HandlerName { get; }

    /// <summary>
    /// Gets the Agent Card security scheme name.
    /// </summary>
    public string SecuritySchemeName { get; }

    /// <summary>
    /// Gets the referenced Agent Card security scheme name, if one was configured.
    /// </summary>
    public string ReferencedSecurityScheme { get; }

    /// <summary>
    /// Gets the inline Agent Card security scheme, if one was configured.
    /// </summary>
    public SecurityScheme SecurityScheme { get; }

    /// <summary>
    /// Gets the Agent Card scopes required by this handler.
    /// </summary>
    public IList<string> RequiredScopes { get; }

    /// <summary>
    /// Gets the authorization policy associated with this handler.
    /// </summary>
    public string AuthorizationPolicy { get; }

    /// <summary>
    /// Gets the OBO settings used when retrieving a request token.
    /// </summary>
    public OBOSettings OBOSettings { get; }

    internal static IReadOnlyList<A2AAuthorizationMetadata> Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var handlers = configuration.GetSection("AgentApplication:UserAuthorization:Handlers");
        var metadata = new List<A2AAuthorizationMetadata>();
        foreach (var handler in handlers.GetChildren())
        {
            var type = handler.GetValue<string>("Type");
            if (string.Equals(type, typeof(A2AUserAuthorization).Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, typeof(A2AUserAuthorization).FullName, StringComparison.OrdinalIgnoreCase))
            {
                metadata.Add(new A2AAuthorizationMetadata(
                    handler.Key,
                    A2AUserAuthorizationSettings.FromConfiguration(handler.GetSection("Settings"))));
            }
        }

        return metadata;
    }
}
