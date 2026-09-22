// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuth21DcrCredentialProvider : IOAuthCredentialProvider
{
    private const int DcrProviderSpecificity = -1;
    private const int DcrRegistrationSpecificity = 0;

    private readonly OAuthCredentialProviderOptions _options;
    private readonly IOAuthAuthorizationServerMetadataClient _metadataClient;
    private readonly IDynamicClientRegistrationClient _registrationClient;
    private readonly IOAuthClientRegistrationStore _registrationStore;
    private readonly IOAuthProviderApproval _approval;

    public OAuth21DcrCredentialProvider(
        OAuthCredentialProviderOptions options,
        IOAuthAuthorizationServerMetadataClient metadataClient,
        IDynamicClientRegistrationClient registrationClient,
        IOAuthClientRegistrationStore registrationStore,
        IOAuthProviderApproval approval)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metadataClient = metadataClient ?? throw new ArgumentNullException(nameof(metadataClient));
        _registrationClient = registrationClient ?? throw new ArgumentNullException(nameof(registrationClient));
        _registrationStore = registrationStore ?? throw new ArgumentNullException(nameof(registrationStore));
        _approval = approval ?? throw new ArgumentNullException(nameof(approval));

        if (_options.Type != OAuthCredentialProviderType.OAuth21PkceDcr)
        {
            throw new InvalidOperationException(
                $"Provider '{_options.Id}' must declare Type '{OAuthCredentialProviderType.OAuth21PkceDcr}'.");
        }

        OAuthEndpointValidator.EnsureSupportedLoopbackRedirectUri(_options.RedirectUri);
    }

    public string Id => _options.Id;

    public OAuthProviderMatch? Match(A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        return authentication.FlowType == A2AOAuthFlowType.AuthorizationCode
            ? new OAuthProviderMatch(
                this,
                RegistrationId: null,
                ProviderSpecificity: DcrProviderSpecificity,
                AuthoritySpecificity: 0,
                RegistrationSpecificity: DcrRegistrationSpecificity)
            : null;
    }

    public async Task<OAuthCredentialBinding> BindAsync(
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

        if (authentication.FlowType != A2AOAuthFlowType.AuthorizationCode)
        {
            throw new InvalidOperationException(
                $"OAuth provider '{Id}' only supports flow '{A2AOAuthFlowType.AuthorizationCode}'.");
        }

        Uri redirectUri = _options.RedirectUri!;
        OAuthAuthorizationServerMetadata metadata = await DiscoverMetadataAsync(
            agentOrigin,
            authentication,
            cancellationToken).ConfigureAwait(false);
        string providerIdentity = metadata.Issuer.AbsoluteUri;

        OAuthClientRegistration? storedRegistration = await _registrationStore.GetAsync(
            providerIdentity,
            redirectUri,
            cancellationToken).ConfigureAwait(false);
        if (storedRegistration is not null)
        {
            EnsureStoredRegistrationCompatible(storedRegistration, providerIdentity);
            return CreateBinding(authentication, metadata, storedRegistration, providerIdentity);
        }

        if (!_options.AllowInteractiveApproval)
        {
            throw new InvalidOperationException(
                $"OAuth provider '{Id}' has no stored registration for issuer '{providerIdentity}', and interactive approval is disabled.");
        }

        bool approved = await _approval.ApproveAsync(
            new OAuthProviderApprovalRequest(
                agentOrigin,
                authentication.SecuritySchemeName,
                authentication.Scopes,
                GetAdvertisedEndpoints(authentication),
                metadata,
                redirectUri),
            cancellationToken).ConfigureAwait(false);
        if (!approved)
        {
            throw new InvalidOperationException(
                $"OAuth provider '{Id}' was not approved for dynamic client registration with issuer '{providerIdentity}'.");
        }

        OAuthClientRegistration registration = await _registrationClient.RegisterPublicClientAsync(
            registrationId: "dynamic",
            registrationEndpoint: metadata.RegistrationEndpoint,
            redirectUri: redirectUri,
            cancellationToken).ConfigureAwait(false);
        EnsureStoredRegistrationCompatible(registration, providerIdentity);
        await _registrationStore.SaveAsync(
            providerIdentity,
            redirectUri,
            registration,
            cancellationToken).ConfigureAwait(false);

        return CreateBinding(authentication, metadata, registration, providerIdentity);
    }

    private async Task<OAuthAuthorizationServerMetadata> DiscoverMetadataAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Uri> discoveryCandidates = GetDiscoveryCandidates(authentication);
        Exception? discoveryFailure = null;

        if (discoveryCandidates.Count > 0)
        {
            try
            {
                return await _metadataClient.DiscoverAsync(discoveryCandidates, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException
                && !cancellationToken.IsCancellationRequested)
            {
                discoveryFailure = exception;
            }
        }

        if (_options.ServerUrl is not null)
        {
            IReadOnlyList<Uri> configuredServerCandidates =
                OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromIssuer(_options.ServerUrl);

            try
            {
                return await _metadataClient.DiscoverAsync(configuredServerCandidates, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException
                && !cancellationToken.IsCancellationRequested)
            {
                discoveryFailure = exception;
            }
        }

        if (_options.AllowInteractiveApproval)
        {
            Uri? serverUri = await _approval.RequestServerUriAsync(
                agentOrigin,
                GetAdvertisedEndpoints(authentication),
                cancellationToken).ConfigureAwait(false);
            if (serverUri is null)
            {
                throw new InvalidOperationException(
                    $"OAuth provider '{Id}' could not discover metadata and no server URI was provided for dynamic client registration.");
            }

            IReadOnlyList<Uri> promptedCandidates = OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromIssuer(serverUri);
            return await _metadataClient.DiscoverAsync(promptedCandidates, cancellationToken).ConfigureAwait(false);
        }

        throw discoveryFailure ?? new InvalidOperationException(
            $"OAuth provider '{Id}' could not discover OAuth metadata because no valid discovery candidates were available.");
    }

    private IReadOnlyList<Uri> GetDiscoveryCandidates(A2AAgentCardAuthentication authentication)
    {
        var candidates = new List<Uri>();
        if (_options.MetadataUrl is not null)
        {
            candidates.AddRange(OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromMetadataUrl(_options.MetadataUrl));
        }

        if (!string.IsNullOrWhiteSpace(authentication.MetadataUrl))
        {
            TryAddMetadataCandidates(authentication.MetadataUrl, candidates);
        }

        foreach (Uri endpoint in GetOriginEndpoints(authentication).Distinct())
        {
            candidates.AddRange(OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromOrigin(endpoint));
        }

        return OAuthAuthorizationServerMetadataClient.DeduplicateMetadataCandidates(candidates);
    }

    private static void TryAddMetadataCandidates(string? metadataUrl, List<Uri> candidates)
    {
        if (TryGetAdvertisedEndpoint(metadataUrl, "metadata URL", out Uri? metadataEndpoint)
            && metadataEndpoint is not null)
        {
            candidates.AddRange(
                OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromMetadataUrl(metadataEndpoint));
        }
    }

    private static IReadOnlyList<Uri> GetOriginEndpoints(A2AAgentCardAuthentication authentication)
    {
        var endpoints = new List<Uri>();
        if (TryGetAdvertisedEndpoint(authentication.AuthorizationUrl, "authorization endpoint", out Uri? authorizationEndpoint)
            && authorizationEndpoint is not null)
        {
            endpoints.Add(authorizationEndpoint);
        }

        if (TryGetAdvertisedEndpoint(authentication.TokenUrl, "token endpoint", out Uri? tokenEndpoint)
            && tokenEndpoint is not null)
        {
            endpoints.Add(tokenEndpoint);
        }

        return endpoints;
    }

    private static IReadOnlyList<Uri> GetAdvertisedEndpoints(A2AAgentCardAuthentication authentication)
    {
        var endpoints = new List<Uri>();
        if (TryGetAdvertisedEndpoint(authentication.AuthorizationUrl, "authorization endpoint", out Uri? authorizationEndpoint)
            && authorizationEndpoint is not null)
        {
            endpoints.Add(authorizationEndpoint);
        }

        if (TryGetAdvertisedEndpoint(authentication.TokenUrl, "token endpoint", out Uri? tokenEndpoint)
            && tokenEndpoint is not null)
        {
            endpoints.Add(tokenEndpoint);
        }

        return endpoints;
    }

    private static bool TryGetAdvertisedEndpoint(
        string? value,
        string endpointName,
        out Uri? endpoint)
    {
        try
        {
            endpoint = OAuthEndpointValidator.GetAdvertisedEndpoint(value, endpointName);
            return true;
        }
        catch (InvalidOperationException)
        {
            endpoint = null;
            return false;
        }
    }

    private static void EnsureStoredRegistrationCompatible(
        OAuthClientRegistration registration,
        string providerIdentity)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerIdentity);

        if (!registration.GrantTypes.Contains(A2AOAuthFlowType.AuthorizationCode))
        {
            throw new InvalidOperationException(
                $"Stored OAuth registration for issuer '{providerIdentity}' does not support Authorization Code.");
        }

        if (string.IsNullOrWhiteSpace(registration.ClientId))
        {
            throw new InvalidOperationException(
                $"Stored OAuth registration for issuer '{providerIdentity}' must include ClientId.");
        }

        if (registration.ClientSecret is not null)
        {
            throw new InvalidOperationException(
                $"Stored OAuth registration for issuer '{providerIdentity}' must not include ClientSecret.");
        }

        if (registration.TokenEndpointAuthenticationMethod != OAuthTokenEndpointAuthenticationMethod.None)
        {
            throw new InvalidOperationException(
                $"Stored OAuth registration for issuer '{providerIdentity}' must use token endpoint authentication method '{OAuthTokenEndpointAuthenticationMethod.None}'.");
        }

        OAuthEndpointValidator.EnsureSupportedLoopbackRedirectUri(registration.RedirectUri);
        if (!registration.UsePkce)
        {
            throw new InvalidOperationException(
                $"Stored OAuth registration for issuer '{providerIdentity}' must use PKCE.");
        }
    }

    private OAuthCredentialBinding CreateBinding(
        A2AAgentCardAuthentication authentication,
        OAuthAuthorizationServerMetadata metadata,
        OAuthClientRegistration registration,
        string providerIdentity)
    {
        if (metadata.AuthorizationEndpoint is null)
        {
            throw new InvalidOperationException(
                $"Discovered OAuth metadata for issuer '{providerIdentity}' does not include an authorization endpoint.");
        }

        return new(
            ProviderId: Id,
            ProviderType: _options.Type,
            RegistrationId: registration.ClientId,
            Registration: registration,
            FlowType: A2AOAuthFlowType.AuthorizationCode,
            AuthorizationEndpoint: metadata.AuthorizationEndpoint,
            DeviceAuthorizationEndpoint: null,
            TokenEndpoint: metadata.TokenEndpoint,
            MetadataUrl: metadata.MetadataUrl,
            RegistrationEndpoint: metadata.RegistrationEndpoint,
            EffectiveScopes: OAuthScopeResolver.GetScopes(authentication, _options),
            ProviderIdentity: providerIdentity);
    }
}
