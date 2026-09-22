// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

namespace Microsoft.Agents.Samples.A2AClient;

internal class GenericOAuth2CredentialProvider : IOAuthCredentialProvider
{
    private const int ConfiguredRegistrationSpecificity = 1;
    private readonly OAuthCredentialProviderOptions _options;

    public GenericOAuth2CredentialProvider(OAuthCredentialProviderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (GetType() == typeof(GenericOAuth2CredentialProvider)
            && _options.Type != OAuthCredentialProviderType.GenericOAuth2)
        {
            throw new InvalidOperationException(
                $"Provider '{_options.Id}' must declare Type '{OAuthCredentialProviderType.GenericOAuth2}'.");
        }
    }

    public string Id => _options.Id;

    protected OAuthCredentialProviderOptions Options => _options;

    protected virtual int ProviderSpecificity => 0;

    protected virtual IReadOnlyList<Uri> AllowedAuthorities => _options.AllowedAuthorities;

    protected virtual IReadOnlyList<Uri> AllowedOrigins => _options.AllowedOrigins;

    protected virtual string ProviderIdentity => Id;

    public virtual OAuthProviderMatch? Match(A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        Uri? metadataEndpoint = GetPreferredMetadataEndpoint(authentication);
        Uri? authorizationEndpoint = authentication.FlowType == A2AOAuthFlowType.AuthorizationCode
            ? OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.AuthorizationUrl, "authorization endpoint")
            : null;
        Uri? deviceAuthorizationEndpoint = authentication.FlowType == A2AOAuthFlowType.DeviceCode
            ? OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.DeviceAuthorizationUrl, "device authorization endpoint")
            : null;
        Uri tokenEndpoint = OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.TokenUrl, "token endpoint");
        Uri[] endpoints = [.. GetTrustEvaluationEndpoints(authorizationEndpoint, deviceAuthorizationEndpoint, tokenEndpoint, metadataEndpoint)];
        int? authoritySpecificity = OAuthEndpointValidator.GetCommonTrustMatchSpecificity(
            endpoints,
            AllowedAuthorities,
            AllowedOrigins);
        if (authoritySpecificity is null)
        {
            return null;
        }

        OAuthClientRegistration[] registrations = _options.Registrations.Values
            .Where(registration => registration.GrantTypes.Contains(authentication.FlowType))
            .ToArray();
        OAuthClientRegistration[] compatibleRegistrations = registrations
            .Where(registration => IsRegistrationCompatible(registration, authentication))
            .ToArray();
        if (compatibleRegistrations.Length > 1)
        {
            throw new InvalidOperationException(
                $"OAuth provider '{Id}' has multiple compatible registrations for flow '{authentication.FlowType}': "
                + $"{string.Join(", ", compatibleRegistrations.Select(registration => $"{Id}/{registration.Id}"))}.");
        }

        return new OAuthProviderMatch(
            this,
            compatibleRegistrations.SingleOrDefault()?.Id,
            ProviderSpecificity,
            authoritySpecificity.Value,
            compatibleRegistrations.Length == 1 ? ConfiguredRegistrationSpecificity : -1);
    }

    public virtual Task<OAuthCredentialBinding> BindAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        OAuthProviderMatch match,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentOrigin);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(match);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(match.Provider, this))
        {
            throw new InvalidOperationException(
                $"Provider match '{match.Provider.Id}' cannot bind through provider '{Id}'.");
        }

        Uri? metadataEndpoint = GetPreferredMetadataEndpoint(authentication);
        Uri? authorizationEndpoint = authentication.FlowType == A2AOAuthFlowType.AuthorizationCode
            ? OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.AuthorizationUrl, "authorization endpoint")
            : null;
        Uri? deviceAuthorizationEndpoint = authentication.FlowType == A2AOAuthFlowType.DeviceCode
            ? OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.DeviceAuthorizationUrl, "device authorization endpoint")
            : null;
        Uri tokenEndpoint = OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.TokenUrl, "token endpoint");
        Uri[] endpoints = [.. GetTrustEvaluationEndpoints(authorizationEndpoint, deviceAuthorizationEndpoint, tokenEndpoint, metadataEndpoint)];
        int? authoritySpecificity = OAuthEndpointValidator.GetCommonTrustMatchSpecificity(
            endpoints,
            AllowedAuthorities,
            AllowedOrigins);
        if (authoritySpecificity is null)
        {
            throw new InvalidOperationException(
                $"OAuth provider '{Id}' does not trust the advertised endpoints for flow '{authentication.FlowType}'.");
        }

        if (match.RegistrationId is null)
        {
            throw new InvalidOperationException(GetMissingRegistrationMessage(authentication));
        }

        if (!_options.Registrations.TryGetValue(match.RegistrationId, out OAuthClientRegistration? registration))
        {
            throw new InvalidOperationException(
                $"OAuth provider '{Id}' does not contain registration '{match.RegistrationId}'.");
        }

        if (!registration.GrantTypes.Contains(authentication.FlowType)
            || !IsRegistrationCompatible(registration, authentication))
        {
            throw new InvalidOperationException(GetMissingRegistrationMessage(authentication));
        }

        return Task.FromResult(
            new OAuthCredentialBinding(
                Id,
                _options.Type,
                registration.Id,
                registration,
                authentication.FlowType,
                authorizationEndpoint,
                deviceAuthorizationEndpoint,
                tokenEndpoint,
                metadataEndpoint,
                RegistrationEndpoint: null,
                OAuthScopeResolver.GetScopes(authentication, _options),
                ProviderIdentity));
    }

    protected virtual bool IsRegistrationCompatible(
        OAuthClientRegistration registration,
        A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(authentication);

        if (RequiresClientSecret(registration.TokenEndpointAuthenticationMethod)
            && string.IsNullOrWhiteSpace(registration.ClientSecret))
        {
            return false;
        }

        if (authentication.FlowType == A2AOAuthFlowType.AuthorizationCode
            && !HasPermittedRedirectUri(registration.RedirectUri))
        {
            return false;
        }

        return true;
    }

    protected virtual string GetMissingRegistrationMessage(A2AAgentCardAuthentication authentication)
        => $"OAuth provider '{Id}' has no compatible registration for flow '{authentication.FlowType}'.";

    private Uri? GetPreferredMetadataEndpoint(A2AAgentCardAuthentication authentication)
    {
        if (_options.MetadataUrl is not null)
        {
            return _options.MetadataUrl;
        }

        return string.IsNullOrWhiteSpace(authentication.MetadataUrl)
            ? null
            : OAuthEndpointValidator.GetAdvertisedEndpoint(authentication.MetadataUrl, "metadata URL");
    }

    private static IEnumerable<Uri> GetTrustEvaluationEndpoints(
        Uri? authorizationEndpoint,
        Uri? deviceAuthorizationEndpoint,
        Uri tokenEndpoint,
        Uri? metadataEndpoint)
    {
        if (authorizationEndpoint is not null)
        {
            yield return authorizationEndpoint;
        }

        if (deviceAuthorizationEndpoint is not null)
        {
            yield return deviceAuthorizationEndpoint;
        }

        yield return tokenEndpoint;

        if (metadataEndpoint is not null)
        {
            yield return OAuthAuthorizationServerMetadataClient.GetMetadataTrustAuthority(metadataEndpoint);
        }
    }

    private static bool RequiresClientSecret(OAuthTokenEndpointAuthenticationMethod authenticationMethod)
        => authenticationMethod is OAuthTokenEndpointAuthenticationMethod.ClientSecretBasic
            or OAuthTokenEndpointAuthenticationMethod.ClientSecretPost;

    private static bool HasPermittedRedirectUri(Uri? redirectUri)
        => OAuthEndpointValidator.IsSupportedLoopbackRedirectUri(redirectUri);
}
