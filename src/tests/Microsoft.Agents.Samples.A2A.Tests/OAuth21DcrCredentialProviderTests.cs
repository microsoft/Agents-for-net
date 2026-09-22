// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class OAuth21DcrCredentialProviderTests
{
    private static readonly Uri s_agentOrigin = new("https://agent.example");
    private static readonly Uri s_redirectUri = new("http://localhost:8400/callback/");

    [Fact]
    public void Match_AuthorizationCodeIsRequired()
    {
        var sut = CreateProvider();

        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "device-oauth",
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://identity.example.com/oauth/device",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        Assert.Null(sut.Match(authentication));
    }

    [Fact]
    public async Task ResolveAsync_ConfiguredProviderOutranksDcrFallback()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var metadataClient = new RecordingMetadataClient(
            CreateMetadata(
                issuer: "https://identity.example.com",
                metadataUrl: "https://identity.example.com/.well-known/oauth-authorization-server"));
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval();
        var store = new RecordingRegistrationStore();
        var resolver = new OAuthCredentialProviderResolver(
        [
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "configured",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "configured-client-id",
                            ClientSecret: null,
                            RedirectUri: s_redirectUri,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }),
            new OAuth21DcrCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "dcr",
                    Type = OAuthCredentialProviderType.OAuth21PkceDcr,
                    AllowInteractiveApproval = true,
                    RedirectUri = s_redirectUri,
                },
                metadataClient,
                registrationClient,
                store,
                approval),
        ]);

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("configured", binding.ProviderId);
        Assert.Equal("browser", binding.RegistrationId);
        Assert.Empty(metadataClient.Requests);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
    }

    [Fact]
    public async Task BindAsync_ReusesStoredRegistrationWithoutApprovalOrDynamicRegistration()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://issuer.example/tenant",
            metadataUrl: "https://issuer.example/custom/.well-known/oauth-authorization-server");
        var metadataClient = new RecordingMetadataClient(metadata);
        OAuthClientRegistration storedRegistration = CreateDynamicRegistration(clientId: "stored-client-id");
        var store = new RecordingRegistrationStore(
            CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, storedRegistration));
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval();
        var sut = CreateProvider(
            new OAuthCredentialProviderOptions
            {
                Id = "dcr",
                Type = OAuthCredentialProviderType.OAuth21PkceDcr,
                AllowInteractiveApproval = true,
                RedirectUri = s_redirectUri,
                MetadataUrl = metadata.MetadataUrl,
            },
            metadataClient,
            registrationClient,
            store,
            approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "arbitrary-scheme",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://untrusted.example/oauth/authorize",
                    TokenUrl = "https://untrusted.example/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthCredentialBinding binding = await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        Assert.Equal(
        [
            metadata.MetadataUrl,
            new Uri("https://untrusted.example/.well-known/oauth-authorization-server"),
            new Uri("https://untrusted.example/.well-known/openid-configuration"),
        ],
        Assert.Single(metadataClient.Requests));
        Assert.Equal(storedRegistration.ClientId, binding.Registration.ClientId);
        Assert.Equal(storedRegistration.ClientId, binding.RegistrationId);
        Assert.Equal(metadata.Issuer.AbsoluteUri, binding.ProviderIdentity);
        Assert.Equal(metadata.AuthorizationEndpoint, binding.AuthorizationEndpoint);
        Assert.Equal(metadata.TokenEndpoint, binding.TokenEndpoint);
        Assert.Equal(metadata.MetadataUrl, binding.MetadataUrl);
        Assert.Equal(metadata.RegistrationEndpoint, binding.RegistrationEndpoint);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task BindAsync_UsesAdvertisedMetadataUrlBeforeEndpointOrigins()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://identity.example.com",
            metadataUrl: "https://identity.example.com/tenant/.well-known/openid-configuration");
        var metadataClient = new RecordingMetadataClient(metadata);
        var sut = CreateProvider(
            metadataClient: metadataClient,
            registrationStore: new RecordingRegistrationStore(
                CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration())));
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"],
            metadataUrl: metadata.MetadataUrl.AbsoluteUri);

        await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        Assert.Equal(
        [
            metadata.MetadataUrl,
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://identity.example.com/.well-known/openid-configuration"),
        ],
        Assert.Single(metadataClient.Requests));
    }

    [Fact]
    public async Task BindAsync_FallsBackToAdvertisedEndpointOriginsWhenNoMetadataUrlExists()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://identity.example.com",
            metadataUrl: "https://identity.example.com/.well-known/oauth-authorization-server");
        var metadataClient = new RecordingMetadataClient(metadata);
        var sut = CreateProvider(
            metadataClient: metadataClient,
            registrationStore: new RecordingRegistrationStore(
                CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration())));
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://login.identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        Assert.Equal(
        [
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://identity.example.com/.well-known/openid-configuration"),
            new Uri("https://login.identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://login.identity.example.com/.well-known/openid-configuration"),
        ],
        Assert.Single(metadataClient.Requests));
    }

    [Fact]
    public async Task BindAsync_InvalidAdvertisedDiscoveryInputs_FallThroughToPrompt()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://issuer.example/tenant",
            metadataUrl: "https://issuer.example/.well-known/oauth-authorization-server/tenant");
        var metadataClient = new RecordingMetadataClient(metadata);
        var approval = new RecordingApproval
        {
            RequestedServerUri = new Uri("https://issuer.example/tenant"),
        };
        var sut = CreateProvider(
            metadataClient: metadataClient,
            registrationStore: new RecordingRegistrationStore(
                CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration())),
            approval: approval);
        A2AAgentCardAuthentication authentication = A2AAgentCardAuthentication.CreateInTask(
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "http://identity.example.com/oauth/authorize",
                    TokenUrl = "http://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthCredentialBinding binding = await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        Assert.Equal(1, approval.RequestServerUriCallCount);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(
        [
            new Uri("https://issuer.example/.well-known/oauth-authorization-server/tenant"),
            new Uri("https://issuer.example/tenant/.well-known/openid-configuration"),
        ],
        Assert.Single(metadataClient.Requests));
        Assert.Equal(metadata.Issuer.AbsoluteUri, binding.ProviderIdentity);
    }

    [Fact]
    public async Task BindAsync_ConfiguredServerUrlIsTriedBeforePrompt_AndSuppressesPromptOnSuccess()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://configured.example/tenant",
            metadataUrl: "https://configured.example/.well-known/oauth-authorization-server/tenant");
        var metadataClient = new RecordingMetadataClient(
            new InvalidOperationException("initial discovery failed"),
            metadata);
        var approval = new RecordingApproval
        {
            RequestedServerUri = new Uri("https://prompted.example/tenant"),
        };
        var sut = CreateProvider(
            options: new OAuthCredentialProviderOptions
            {
                Id = "dcr",
                Type = OAuthCredentialProviderType.OAuth21PkceDcr,
                AllowInteractiveApproval = true,
                RedirectUri = s_redirectUri,
                ServerUrl = new Uri("https://configured.example/tenant"),
            },
            metadataClient: metadataClient,
            registrationStore: new RecordingRegistrationStore(
                CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration())),
            approval: approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthCredentialBinding binding = await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        Assert.Equal(0, approval.RequestServerUriCallCount);
        Assert.Equal(
        [
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://identity.example.com/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[0]);
        Assert.Equal(
        [
            new Uri("https://configured.example/.well-known/oauth-authorization-server/tenant"),
            new Uri("https://configured.example/tenant/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[1]);
        Assert.Equal(metadata.Issuer.AbsoluteUri, binding.ProviderIdentity);
    }

    [Fact]
    public async Task BindAsync_ConfiguredServerUrlFailure_FallsThroughToPrompt()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://prompted.example/tenant",
            metadataUrl: "https://prompted.example/.well-known/oauth-authorization-server/tenant");
        var metadataClient = new RecordingMetadataClient(
            new InvalidOperationException("initial discovery failed"),
            new InvalidOperationException("configured server discovery failed"),
            metadata);
        var approval = new RecordingApproval
        {
            RequestedServerUri = new Uri("https://prompted.example/tenant"),
        };
        var sut = CreateProvider(
            options: new OAuthCredentialProviderOptions
            {
                Id = "dcr",
                Type = OAuthCredentialProviderType.OAuth21PkceDcr,
                AllowInteractiveApproval = true,
                RedirectUri = s_redirectUri,
                ServerUrl = new Uri("https://configured.example/tenant"),
            },
            metadataClient: metadataClient,
            registrationStore: new RecordingRegistrationStore(
                CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration())),
            approval: approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthCredentialBinding binding = await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        Assert.Equal(1, approval.RequestServerUriCallCount);
        Assert.Equal(
        [
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://identity.example.com/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[0]);
        Assert.Equal(
        [
            new Uri("https://configured.example/.well-known/oauth-authorization-server/tenant"),
            new Uri("https://configured.example/tenant/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[1]);
        Assert.Equal(
        [
            new Uri("https://prompted.example/.well-known/oauth-authorization-server/tenant"),
            new Uri("https://prompted.example/tenant/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[2]);
        Assert.Equal(metadata.Issuer.AbsoluteUri, binding.ProviderIdentity);
    }

    [Fact]
    public async Task BindAsync_DiscoveryFailurePromptsForServerUri_AndDeclinedApprovalStopsBeforeRegistration()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://issuer.example/tenant",
            metadataUrl: "https://issuer.example/.well-known/oauth-authorization-server/tenant");
        var metadataClient = new RecordingMetadataClient(
            new InvalidOperationException("initial discovery failed"),
            metadata);
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval
        {
            RequestedServerUri = new Uri("https://issuer.example/tenant"),
            ApprovalResult = false,
        };
        var store = new RecordingRegistrationStore();
        var sut = CreateProvider(
            metadataClient: metadataClient,
            registrationClient: registrationClient,
            registrationStore: store,
            approval: approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.BindAsync(
                s_agentOrigin,
                authentication,
                Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
                CancellationToken.None));

        Assert.Equal(1, approval.RequestServerUriCallCount);
        Assert.Equal(1, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
        Assert.Empty(store.Saved);
        Assert.Equal(
        [
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://identity.example.com/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[0]);
        Assert.Equal(
        [
            new Uri("https://issuer.example/.well-known/oauth-authorization-server/tenant"),
            new Uri("https://issuer.example/tenant/.well-known/openid-configuration"),
        ],
        metadataClient.Requests[1]);
        Assert.Contains("not approved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BindAsync_ApprovesRegistersAndSavesBindingUsingDiscoveredIssuerIdentity()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://issuer.example/tenant",
            metadataUrl: "https://issuer.example/.well-known/oauth-authorization-server/tenant");
        OAuthClientRegistration registration = CreateDynamicRegistration(clientId: "registered-client-id");
        var metadataClient = new RecordingMetadataClient(metadata);
        var registrationClient = new RecordingDynamicClientRegistrationClient(registration);
        var approval = new RecordingApproval
        {
            ApprovalResult = true,
        };
        var store = new RecordingRegistrationStore();
        var sut = CreateProvider(
            metadataClient: metadataClient,
            registrationClient: registrationClient,
            registrationStore: store,
            approval: approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthCredentialBinding binding = await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        OAuthProviderApprovalRequest request = Assert.IsType<OAuthProviderApprovalRequest>(approval.LastApprovalRequest);
        Assert.Equal(s_agentOrigin, request.AgentOrigin);
        Assert.Equal("browser-oauth", request.SecuritySchemeName);
        Assert.Equal(["repo.read"], request.Scopes);
        Assert.Equal(
        [
            new Uri("https://identity.example.com/oauth/authorize"),
            new Uri("https://identity.example.com/oauth/token"),
        ],
        request.AdvertisedEndpoints);
        Assert.Equal(metadata, request.Metadata);
        Assert.Equal(s_redirectUri, request.RedirectUri);

        Assert.Equal(1, registrationClient.RegisterCallCount);
        Assert.Equal(metadata.RegistrationEndpoint, registrationClient.RegistrationEndpoint);
        Assert.Equal(s_redirectUri, registrationClient.RedirectUri);

        RecordingRegistrationStore.SaveCall save = Assert.Single(store.Saved);
        Assert.Equal(metadata.Issuer.AbsoluteUri, save.ProviderIdentity);
        Assert.Equal(s_redirectUri, save.RedirectUri);
        Assert.Equal(registration, save.Registration);

        Assert.Equal("dcr", binding.ProviderId);
        Assert.Equal(registration.ClientId, binding.RegistrationId);
        Assert.Equal(registration, binding.Registration);
        Assert.Equal(metadata.Issuer.AbsoluteUri, binding.ProviderIdentity);
        Assert.Equal(metadata.AuthorizationEndpoint, binding.AuthorizationEndpoint);
        Assert.Equal(metadata.TokenEndpoint, binding.TokenEndpoint);
        Assert.Equal(metadata.MetadataUrl, binding.MetadataUrl);
        Assert.Equal(metadata.RegistrationEndpoint, binding.RegistrationEndpoint);
    }

    [Fact]
    public async Task ConsoleApproval_ApproveAsync_PrintsAllRequestDetails_AndAcceptsExplicitYes()
    {
        var input = new StringReader("yes" + Environment.NewLine);
        var output = new StringWriter();
        var sut = new ConsoleOAuthProviderApproval(input, output);
        OAuthProviderApprovalRequest request = new(
            s_agentOrigin,
            "browser-oauth",
            ["repo.read", "offline_access"],
            [
                new Uri("https://identity.example.com/oauth/authorize"),
                new Uri("https://identity.example.com/oauth/token"),
            ],
            CreateMetadata(
                issuer: "https://issuer.example/tenant",
                metadataUrl: "https://issuer.example/.well-known/oauth-authorization-server/tenant"),
            s_redirectUri);

        bool approved = await sut.ApproveAsync(request, CancellationToken.None);

        string text = output.ToString();
        Assert.True(approved);
        Assert.Contains(s_agentOrigin.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains("browser-oauth", text, StringComparison.Ordinal);
        Assert.Contains("repo.read", text, StringComparison.Ordinal);
        Assert.Contains("offline_access", text, StringComparison.Ordinal);
        Assert.Contains("https://identity.example.com/oauth/authorize", text, StringComparison.Ordinal);
        Assert.Contains("https://identity.example.com/oauth/token", text, StringComparison.Ordinal);
        Assert.Contains("https://issuer.example/tenant", text, StringComparison.Ordinal);
        Assert.Contains("https://issuer.example/.well-known/oauth-authorization-server/tenant", text, StringComparison.Ordinal);
        Assert.Contains("https://issuer.example/tenant/oauth/register", text, StringComparison.Ordinal);
        Assert.Contains(s_redirectUri.AbsoluteUri, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsoleApproval_RequestServerUriAsync_RepeatsUntilAbsoluteHttpsUri()
    {
        var input = new StringReader(string.Join(
            Environment.NewLine,
            [
                "not-a-uri",
                "http://issuer.example/tenant",
                "https://issuer.example/tenant",
                string.Empty,
            ]) + Environment.NewLine);
        var output = new StringWriter();
        var sut = new ConsoleOAuthProviderApproval(input, output);

        Uri? serverUri = await sut.RequestServerUriAsync(
            s_agentOrigin,
            [
                new Uri("https://identity.example.com/oauth/authorize"),
                new Uri("https://identity.example.com/oauth/token"),
            ],
            CancellationToken.None);

        Assert.Equal(new Uri("https://issuer.example/tenant"), serverUri);
        string text = output.ToString();
        Assert.Contains(s_agentOrigin.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains("https://identity.example.com/oauth/authorize", text, StringComparison.Ordinal);
        Assert.Contains("https://identity.example.com/oauth/token", text, StringComparison.Ordinal);
        Assert.Contains("absolute HTTPS URI", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsoleApproval_RequestServerUriAsync_EmptyLineCancels()
    {
        var sut = new ConsoleOAuthProviderApproval(
            new StringReader(Environment.NewLine),
            new StringWriter());

        Uri? serverUri = await sut.RequestServerUriAsync(
            s_agentOrigin,
            [new Uri("https://identity.example.com/oauth/token")],
            CancellationToken.None);

        Assert.Null(serverUri);
    }

    [Fact]
    public async Task ConsoleApproval_RequestServerUriAsync_UsesCancelableRead()
    {
        var reader = new CancelAwareTextReader();
        var sut = new ConsoleOAuthProviderApproval(reader, new StringWriter());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RequestServerUriAsync(
                s_agentOrigin,
                [new Uri("https://identity.example.com/oauth/token")],
                cancellationTokenSource.Token));

        Assert.True(reader.CancellationAwareReadUsed);
    }

    [Fact]
    public void Match_PinnedAuthorityUsesCommonSpecificityOverAdvertisedEndpoints()
    {
        var sut = CreateProvider(
            CreateDcrOptions(allowedAuthorities: [new Uri("https://identity.example.com/tenant")]));
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/tenant/oauth/authorize",
                    TokenUrl = "https://identity.example.com/tenant/oauth/token",
                },
            },
            ["repo.read"],
            metadataUrl: "https://identity.example.com/tenant/.well-known/oauth-authorization-server");

        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        Assert.Equal(2, match.AuthoritySpecificity);
    }

    [Fact]
    public void Match_HostWideAuthorityRanksBelowPathSpecificAuthority()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/tenant/oauth/authorize",
                    TokenUrl = "https://identity.example.com/tenant/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthProviderMatch hostWide = Assert.IsType<OAuthProviderMatch>(
            CreateProvider(CreateDcrOptions(allowedAuthorities: [new Uri("https://identity.example.com")]))
                .Match(authentication));
        OAuthProviderMatch originOnly = Assert.IsType<OAuthProviderMatch>(
            CreateProvider(CreateDcrOptions(allowedOrigins: [new Uri("https://identity.example.com")]))
                .Match(authentication));

        Assert.Equal(1, hostWide.AuthoritySpecificity);
        Assert.Equal(0, originOnly.AuthoritySpecificity);
    }

    [Fact]
    public void Match_UntrustedAdvertisedEndpointsDoNotMatchPinnedProvider()
    {
        var sut = CreateProvider(
            CreateDcrOptions(allowedAuthorities: [new Uri("https://identity.example.com/tenant")]));
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/other/oauth/authorize",
                    TokenUrl = "https://identity.example.com/other/oauth/token",
                },
            },
            ["repo.read"]);

        Assert.Null(sut.Match(authentication));
    }

    [Fact]
    public void Match_AdvertisedMetadataUrlOutsideTrustPolicyDoesNotMatch()
    {
        var sut = CreateProvider(
            CreateDcrOptions(allowedOrigins: [new Uri("https://identity.example.com")]));
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"],
            metadataUrl: "https://malicious.example/.well-known/oauth-authorization-server");

        Assert.Null(sut.Match(authentication));
    }

    [Fact]
    public void Match_OpenProviderWithoutTrustListsMatchesAnyAuthorizationCodeFlow()
    {
        var sut = CreateProvider(CreateDcrOptions());
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        Assert.Null(match.RegistrationId);
        Assert.Equal(-1, match.ProviderSpecificity);
    }

    [Fact]
    public async Task BindAsync_RejectsDiscoveredEndpointsOutsideTrustPolicyBeforeApproval()
    {
        var metadataClient = new RecordingMetadataClient(
            CreateMetadata(
                issuer: "https://malicious.example",
                metadataUrl: "https://malicious.example/.well-known/oauth-authorization-server"));
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval();
        var store = new RecordingRegistrationStore();
        var sut = CreateProvider(
            CreateDcrOptions(
                allowedOrigins: [new Uri("https://identity.example.com")],
                metadataUrl: new Uri("https://identity.example.com/.well-known/oauth-authorization-server")),
            metadataClient,
            registrationClient,
            store,
            approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.BindAsync(
                s_agentOrigin,
                authentication,
                Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
                CancellationToken.None));

        Assert.Contains("dcr", exception.Message, StringComparison.Ordinal);
        Assert.Contains("https://malicious.example", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task BindAsync_RejectsDiscoveredRegistrationEndpointOutsideTrustPolicy()
    {
        var metadataClient = new RecordingMetadataClient(
            new OAuthAuthorizationServerMetadata(
                new Uri("https://identity.example.com"),
                new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
                new Uri("https://identity.example.com/oauth/authorize"),
                new Uri("https://identity.example.com/oauth/token"),
                new Uri("https://malicious.example/oauth/register"),
                ["authorization_code"],
                ["S256"]));
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval();
        var sut = CreateProvider(
            CreateDcrOptions(allowedOrigins: [new Uri("https://identity.example.com")]),
            metadataClient,
            registrationClient,
            new RecordingRegistrationStore(),
            approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.BindAsync(
                s_agentOrigin,
                authentication,
                Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
                CancellationToken.None));

        Assert.Contains("https://malicious.example/oauth/register", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
    }

    [Fact]
    public async Task ResolveAsync_PathSpecificDcrProviderOutranksHostWideDcrProviderWithoutAmbiguity()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/tenant/oauth/authorize",
                    TokenUrl = "https://identity.example.com/tenant/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://identity.example.com/tenant",
            metadataUrl: "https://identity.example.com/.well-known/oauth-authorization-server/tenant");
        var pinnedMetadataClient = new RecordingMetadataClient(metadata);
        var hostWideMetadataClient = new RecordingMetadataClient(metadata);
        var resolver = new OAuthCredentialProviderResolver(
        [
            new OAuth21DcrCredentialProvider(
                CreateDcrOptions(id: "dcr-host", allowedOrigins: [new Uri("https://identity.example.com")]),
                hostWideMetadataClient,
                new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
                new RecordingRegistrationStore(),
                new RecordingApproval()),
            new OAuth21DcrCredentialProvider(
                CreateDcrOptions(id: "dcr-tenant", allowedAuthorities: [new Uri("https://identity.example.com/tenant")]),
                pinnedMetadataClient,
                new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
                new RecordingRegistrationStore(),
                new RecordingApproval()),
        ]);

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("dcr-tenant", binding.ProviderId);
        Assert.Empty(hostWideMetadataClient.Requests);
    }

    [Fact]
    public async Task ResolveAsync_TrustedConfiguredProviderWithoutRegistrationDoesNotFallBackToDcr()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var metadataClient = new RecordingMetadataClient(CreateMetadata());
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval();
        var resolver = new OAuthCredentialProviderResolver(
        [
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "configured",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "device",
                            [A2AOAuthFlowType.DeviceCode],
                            "configured-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }),
            new OAuth21DcrCredentialProvider(
                CreateDcrOptions(),
                metadataClient,
                registrationClient,
                new RecordingRegistrationStore(),
                approval),
        ]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("configured", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(A2AOAuthFlowType.AuthorizationCode), exception.Message, StringComparison.Ordinal);
        Assert.Empty(metadataClient.Requests);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
    }

    [Fact]
    public async Task ResolveAsync_ConfiguredProviderThatDoesNotMatchStillAllowsDcr()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var metadataClient = new RecordingMetadataClient(CreateMetadata());
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval();
        var resolver = new OAuthCredentialProviderResolver(
        [
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "configured",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://other-identity.example")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "configured-client-id",
                            ClientSecret: null,
                            RedirectUri: s_redirectUri,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }),
            new OAuth21DcrCredentialProvider(
                CreateDcrOptions(),
                metadataClient,
                registrationClient,
                new RecordingRegistrationStore(),
                approval),
        ]);

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("dcr", binding.ProviderId);
        Assert.Equal(1, approval.ApproveCallCount);
        Assert.Equal(1, registrationClient.RegisterCallCount);
    }

    [Fact]
    public async Task BindAsync_RepeatedBindsDiscoverMetadataOnceAndLookUpTheStoreEachTime()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var metadataClient = new RecordingMetadataClient(metadata);
        OAuthClientRegistration storedRegistration = CreateDynamicRegistration(clientId: "stored-client-id");
        var store = new RecordingRegistrationStore(
            CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, storedRegistration));
        var approval = new RecordingApproval();
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var sut = CreateProvider(CreateDcrOptions(), metadataClient, registrationClient, store, approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        OAuthCredentialBinding first = await sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);
        OAuthCredentialBinding second = await sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);

        Assert.Single(metadataClient.Requests);
        Assert.Equal(2, store.Lookups.Count);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
        Assert.Equal(storedRegistration, first.Registration);
        Assert.Equal(storedRegistration, second.Registration);
    }

    [Fact]
    public async Task BindAsync_SecondBindAfterDynamicRegistrationDoesNotDiscoverOrApproveAgain()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var metadataClient = new RecordingMetadataClient(metadata);
        var store = new RecordingRegistrationStore();
        var approval = new RecordingApproval();
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var sut = CreateProvider(CreateDcrOptions(), metadataClient, registrationClient, store, approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        await sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);
        await sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);

        Assert.Single(metadataClient.Requests);
        Assert.Equal(1, approval.ApproveCallCount);
        Assert.Equal(1, registrationClient.RegisterCallCount);
        Assert.Single(store.Saved);
        Assert.Equal(2, store.Lookups.Count);
    }

    [Fact]
    public async Task BindAsync_ConcurrentBindsShareOneMetadataDiscovery()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryCompletion = new TaskCompletionSource<OAuthAuthorizationServerMetadata>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var metadataClient = new GatedMetadataClient(discoveryStarted, discoveryCompletion.Task);
        OAuthClientRegistration storedRegistration = CreateDynamicRegistration(clientId: "stored-client-id");
        var store = new RecordingRegistrationStore(
            CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, storedRegistration));
        var sut = CreateProvider(
            CreateDcrOptions(),
            metadataClient,
            new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
            store,
            new RecordingApproval());
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        Task<OAuthCredentialBinding> first = sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);
        await discoveryStarted.Task;
        Task<OAuthCredentialBinding> second = sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);
        discoveryCompletion.SetResult(metadata);
        OAuthCredentialBinding[] bindings = await Task.WhenAll(first, second);

        Assert.Equal(1, metadataClient.CallCount);
        Assert.Equal(2, store.Lookups.Count);
        Assert.All(bindings, binding => Assert.Equal(storedRegistration, binding.Registration));
    }

    [Fact]
    public async Task BindAsync_CancellationWhileDiscoveryIsInFlightIsObserved()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryCompletion = new TaskCompletionSource<OAuthAuthorizationServerMetadata>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var metadataClient = new GatedMetadataClient(discoveryStarted, discoveryCompletion.Task);
        var store = new RecordingRegistrationStore(
            CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration()));
        var sut = CreateProvider(
            CreateDcrOptions(),
            metadataClient,
            new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
            store,
            new RecordingApproval());
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        Task<OAuthCredentialBinding> first = sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None);
        await discoveryStarted.Task;
        using var cancellationTokenSource = new CancellationTokenSource();
        Task<OAuthCredentialBinding> second = sut.BindAsync(
            s_agentOrigin,
            authentication,
            match,
            cancellationTokenSource.Token);
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);

        discoveryCompletion.SetResult(metadata);
        await first;
        Assert.Equal(1, metadataClient.CallCount);
    }

    [Fact]
    public async Task BindAsync_FailedDiscoveryIsNotMemoized()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var metadataClient = new RecordingMetadataClient(
            new InvalidOperationException("Failed to discover OAuth authorization server metadata."),
            metadata);
        var store = new RecordingRegistrationStore(
            CreateStoreEntry(metadata.Issuer.AbsoluteUri, s_redirectUri, CreateDynamicRegistration()));
        var sut = CreateProvider(
            CreateDcrOptions(),
            metadataClient,
            new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
            store,
            new RecordingApproval());
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthProviderMatch match = Assert.IsType<OAuthProviderMatch>(sut.Match(authentication));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.BindAsync(s_agentOrigin, authentication, match, CancellationToken.None));
        OAuthCredentialBinding binding = await sut.BindAsync(
            s_agentOrigin,
            authentication,
            match,
            CancellationToken.None);

        Assert.Equal(2, metadataClient.Requests.Count);
        Assert.Equal(metadata.Issuer.AbsoluteUri, binding.ProviderIdentity);
    }

    [Fact]
    public async Task BindAsync_ApprovalRequestCarriesEveryAdvertisedAndDiscoveredEndpoint()
    {
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var approval = new RecordingApproval();
        var sut = CreateProvider(
            CreateDcrOptions(),
            new RecordingMetadataClient(metadata),
            new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
            new RecordingRegistrationStore(),
            approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"],
            metadataUrl: "https://identity.example.com/.well-known/oauth-authorization-server");

        await sut.BindAsync(
            s_agentOrigin,
            authentication,
            Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
            CancellationToken.None);

        OAuthProviderApprovalRequest request = Assert.IsType<OAuthProviderApprovalRequest>(approval.LastApprovalRequest);
        Assert.Equal(
        [
            new Uri("https://identity.example.com/oauth/authorize"),
            new Uri("https://identity.example.com/oauth/token"),
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
        ],
        request.AdvertisedEndpoints);
        Assert.Equal(metadata.Issuer, request.Metadata.Issuer);
        Assert.Equal(metadata.MetadataUrl, request.Metadata.MetadataUrl);
        Assert.Equal(metadata.AuthorizationEndpoint, request.Metadata.AuthorizationEndpoint);
        Assert.Equal(metadata.TokenEndpoint, request.Metadata.TokenEndpoint);
        Assert.Equal(metadata.RegistrationEndpoint, request.Metadata.RegistrationEndpoint);
        Assert.Equal(s_redirectUri, request.RedirectUri);
    }

    [Fact]
    public async Task ConsoleApproval_ApproveAsync_DisplaysEveryEndpointThatDefinesTheApprovedTrustBoundary()
    {
        var output = new StringWriter();
        var sut = new ConsoleOAuthProviderApproval(new StringReader("y" + Environment.NewLine), output);
        OAuthAuthorizationServerMetadata metadata = CreateMetadata(
            issuer: "https://issuer.example/tenant",
            metadataUrl: "https://issuer.example/.well-known/oauth-authorization-server/tenant");
        OAuthProviderApprovalRequest request = new(
            s_agentOrigin,
            SecuritySchemeName: null,
            ["repo.read"],
            [
                new Uri("https://identity.example.com/oauth/authorize"),
                new Uri("https://identity.example.com/oauth/token"),
                new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            ],
            metadata,
            s_redirectUri);

        bool approved = await sut.ApproveAsync(request, CancellationToken.None);

        string text = output.ToString();
        Assert.True(approved);
        foreach (Uri advertisedEndpoint in request.AdvertisedEndpoints)
        {
            Assert.Contains(advertisedEndpoint.AbsoluteUri, text, StringComparison.Ordinal);
        }

        Assert.Contains(metadata.Issuer.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains(metadata.MetadataUrl.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains(metadata.AuthorizationEndpoint!.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains(metadata.TokenEndpoint.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains(metadata.RegistrationEndpoint.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.Contains(s_redirectUri.AbsoluteUri, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BindAsync_OpenProviderWithoutApprovalOrStoredRegistrationRefusesToRegister()
    {
        var registrationClient = new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration());
        var approval = new RecordingApproval { ApprovalResult = false };
        var sut = CreateProvider(
            CreateDcrOptions(allowInteractiveApproval: false),
            new RecordingMetadataClient(CreateMetadata()),
            registrationClient,
            new RecordingRegistrationStore(),
            approval);
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.BindAsync(
                s_agentOrigin,
                authentication,
                Assert.IsType<OAuthProviderMatch>(sut.Match(authentication)),
                CancellationToken.None));

        Assert.Contains("interactive approval is disabled", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, approval.ApproveCallCount);
        Assert.Equal(0, registrationClient.RegisterCallCount);
    }

    [Fact]
    public void Match_OpenProviderWithoutTrustListsRanksBelowAPinnedProvider()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);

        OAuthProviderMatch open = Assert.IsType<OAuthProviderMatch>(
            CreateProvider(CreateDcrOptions()).Match(authentication));
        OAuthProviderMatch originPinned = Assert.IsType<OAuthProviderMatch>(
            CreateProvider(CreateDcrOptions(allowedOrigins: [new Uri("https://identity.example.com")]))
                .Match(authentication));

        Assert.Equal(open.ProviderSpecificity, originPinned.ProviderSpecificity);
        Assert.True(open.AuthoritySpecificity < originPinned.AuthoritySpecificity);
    }

    [Fact]
    public async Task ResolveAsync_PinnedDcrProviderOutranksOpenDcrProviderWithoutAmbiguity()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        OAuthAuthorizationServerMetadata metadata = CreateMetadata();
        var openMetadataClient = new RecordingMetadataClient(metadata);
        var pinnedMetadataClient = new RecordingMetadataClient(metadata);
        var resolver = new OAuthCredentialProviderResolver(
        [
            new OAuth21DcrCredentialProvider(
                CreateDcrOptions(id: "dcr-open"),
                openMetadataClient,
                new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
                new RecordingRegistrationStore(),
                new RecordingApproval()),
            new OAuth21DcrCredentialProvider(
                CreateDcrOptions(id: "dcr-pinned", allowedOrigins: [new Uri("https://identity.example.com")]),
                pinnedMetadataClient,
                new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
                new RecordingRegistrationStore(),
                new RecordingApproval()),
        ]);

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("dcr-pinned", binding.ProviderId);
        Assert.Empty(openMetadataClient.Requests);
    }

    private static OAuthCredentialProviderOptions CreateDcrOptions(
        string id = "dcr",
        bool allowInteractiveApproval = true,
        IReadOnlyList<Uri>? allowedAuthorities = null,
        IReadOnlyList<Uri>? allowedOrigins = null,
        Uri? metadataUrl = null,
        Uri? serverUrl = null)
        => new()
        {
            Id = id,
            Type = OAuthCredentialProviderType.OAuth21PkceDcr,
            AllowInteractiveApproval = allowInteractiveApproval,
            RedirectUri = s_redirectUri,
            AllowedAuthorities = allowedAuthorities ?? [],
            AllowedOrigins = allowedOrigins ?? [],
            MetadataUrl = metadataUrl,
            ServerUrl = serverUrl,
        };

    private static OAuth21DcrCredentialProvider CreateProvider(
        OAuthCredentialProviderOptions? options = null,
        IOAuthAuthorizationServerMetadataClient? metadataClient = null,
        IDynamicClientRegistrationClient? registrationClient = null,
        IOAuthClientRegistrationStore? registrationStore = null,
        IOAuthProviderApproval? approval = null)
        => new(
            options ?? new OAuthCredentialProviderOptions
            {
                Id = "dcr",
                Type = OAuthCredentialProviderType.OAuth21PkceDcr,
                AllowInteractiveApproval = true,
                RedirectUri = s_redirectUri,
            },
            metadataClient ?? new RecordingMetadataClient(CreateMetadata()),
            registrationClient ?? new RecordingDynamicClientRegistrationClient(CreateDynamicRegistration()),
            registrationStore ?? new RecordingRegistrationStore(),
            approval ?? new RecordingApproval());

    private static OAuthAuthorizationServerMetadata CreateMetadata(
        string issuer = "https://identity.example.com",
        string metadataUrl = "https://identity.example.com/.well-known/oauth-authorization-server")
        => new(
            new Uri(issuer),
            new Uri(metadataUrl),
            new Uri(new Uri(issuer.TrimEnd('/') + "/"), "oauth/authorize"),
            new Uri(new Uri(issuer.TrimEnd('/') + "/"), "oauth/token"),
            new Uri(new Uri(issuer.TrimEnd('/') + "/"), "oauth/register"),
            ["authorization_code"],
            ["S256"]);

    private static OAuthClientRegistration CreateDynamicRegistration(string clientId = "dynamic-client-id")
        => new(
            "dynamic",
            [A2AOAuthFlowType.AuthorizationCode],
            clientId,
            ClientSecret: null,
            RedirectUri: s_redirectUri,
            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
            UsePkce: true);

    private static IReadOnlyDictionary<string, OAuthClientRegistration> CreateRegistrations(
        params OAuthClientRegistration[] registrations)
    {
        var dictionary = new Dictionary<string, OAuthClientRegistration>(StringComparer.Ordinal);
        foreach (OAuthClientRegistration registration in registrations)
        {
            dictionary.Add(registration.Id, registration);
        }

        return dictionary;
    }

    private static RecordingRegistrationStore.StoreEntry CreateStoreEntry(
        string providerIdentity,
        Uri redirectUri,
        OAuthClientRegistration registration)
        => new(providerIdentity, redirectUri, registration);

    private static A2AAgentCardAuthentication CreateAuthentication(
        string securitySchemeName,
        OAuthFlows flows,
        IReadOnlyList<string> scopes,
        string? metadataUrl = null)
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [securitySchemeName] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = flows,
                        OAuth2MetadataUrl = metadataUrl,
                    },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        [securitySchemeName] = new() { List = [.. scopes] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }

    private sealed class RecordingMetadataClient(params object[] outcomes)
        : IOAuthAuthorizationServerMetadataClient
    {
        private readonly Queue<object> _outcomes = new(outcomes);

        public List<IReadOnlyList<Uri>> Requests { get; } = [];

        public Task<OAuthAuthorizationServerMetadata> DiscoverAsync(
            IReadOnlyList<Uri> metadataCandidates,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(metadataCandidates.ToArray());

            object outcome = _outcomes.Dequeue();
            return outcome switch
            {
                OAuthAuthorizationServerMetadata metadata => Task.FromResult(metadata),
                Exception exception => Task.FromException<OAuthAuthorizationServerMetadata>(exception),
                _ => throw new InvalidOperationException("Unexpected metadata outcome."),
            };
        }
    }

    private sealed class RecordingDynamicClientRegistrationClient(OAuthClientRegistration registration)
        : IDynamicClientRegistrationClient
    {
        public int RegisterCallCount { get; private set; }

        public Uri? RegistrationEndpoint { get; private set; }

        public Uri? RedirectUri { get; private set; }

        public string? RegistrationId { get; private set; }

        public Task<OAuthClientRegistration> RegisterPublicClientAsync(
            string registrationId,
            Uri registrationEndpoint,
            Uri redirectUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegisterCallCount++;
            RegistrationId = registrationId;
            RegistrationEndpoint = registrationEndpoint;
            RedirectUri = redirectUri;
            return Task.FromResult(registration);
        }
    }

    private sealed class RecordingRegistrationStore(params RecordingRegistrationStore.StoreEntry[] entries)
        : IOAuthClientRegistrationStore
    {
        private readonly Dictionary<string, OAuthClientRegistration> _registrations = entries.ToDictionary(
            entry => CreateKey(entry.ProviderIdentity, entry.RedirectUri),
            entry => entry.Registration,
            StringComparer.Ordinal);

        public List<SaveCall> Saved { get; } = [];

        public List<LookupCall> Lookups { get; } = [];

        public Task<OAuthClientRegistration?> GetAsync(
            string providerIdentity,
            Uri redirectUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Lookups)
            {
                Lookups.Add(new LookupCall(providerIdentity, redirectUri));
            }

            _registrations.TryGetValue(CreateKey(providerIdentity, redirectUri), out OAuthClientRegistration? registration);
            return Task.FromResult(registration);
        }

        public Task SaveAsync(
            string providerIdentity,
            Uri redirectUri,
            OAuthClientRegistration registration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _registrations[CreateKey(providerIdentity, redirectUri)] = registration;
            Saved.Add(new SaveCall(providerIdentity, redirectUri, registration));
            return Task.CompletedTask;
        }

        public sealed record StoreEntry(string ProviderIdentity, Uri RedirectUri, OAuthClientRegistration Registration);

        public sealed record SaveCall(string ProviderIdentity, Uri RedirectUri, OAuthClientRegistration Registration);

        public sealed record LookupCall(string ProviderIdentity, Uri RedirectUri);

        private static string CreateKey(string providerIdentity, Uri redirectUri)
            => $"{providerIdentity}|{redirectUri.AbsoluteUri}";
    }

    private sealed class GatedMetadataClient(
        TaskCompletionSource discoveryStarted,
        Task<OAuthAuthorizationServerMetadata> completion)
        : IOAuthAuthorizationServerMetadataClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<OAuthAuthorizationServerMetadata> DiscoverAsync(
            IReadOnlyList<Uri> metadataCandidates,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            discoveryStarted.TrySetResult();
            return await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class RecordingApproval : IOAuthProviderApproval
    {
        public int RequestServerUriCallCount { get; private set; }

        public int ApproveCallCount { get; private set; }

        public Uri? RequestedServerUri { get; init; }

        public bool ApprovalResult { get; init; } = true;

        public OAuthProviderApprovalRequest? LastApprovalRequest { get; private set; }

        public Task<Uri?> RequestServerUriAsync(
            Uri agentOrigin,
            IReadOnlyList<Uri> advertisedEndpoints,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestServerUriCallCount++;
            return Task.FromResult(RequestedServerUri);
        }

        public Task<bool> ApproveAsync(
            OAuthProviderApprovalRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApproveCallCount++;
            LastApprovalRequest = request;
            return Task.FromResult(ApprovalResult);
        }
    }

    private sealed class CancelAwareTextReader : TextReader
    {
        public bool CancellationAwareReadUsed { get; private set; }

        public override Task<string?> ReadLineAsync()
            => throw new InvalidOperationException("Expected the cancellation-aware overload.");

        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            CancellationAwareReadUsed = true;
            return ValueTask.FromCanceled<string?>(cancellationToken);
        }
    }
}
