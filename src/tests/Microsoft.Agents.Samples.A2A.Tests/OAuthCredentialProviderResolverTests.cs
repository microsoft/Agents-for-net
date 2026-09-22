// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class OAuthCredentialProviderResolverTests
{
    private static readonly Uri s_agentOrigin = new("https://agent.example");

    [Fact]
    public async Task ResolveAsync_EntraMatchesTrustedAuthorityWithoutUsingSchemeName()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "arbitrary-scheme",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                },
            },
            ["api://agent/access_as_user"],
            metadataUrl: "https://login.microsoftonline.com/organizations/v2.0/.well-known/openid-configuration");
        var resolver = CreateResolver(
            new EntraOAuthCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "entra",
                    Type = OAuthCredentialProviderType.Entra,
                    AdditionalScopes = ["offline_access", "api://agent/access_as_user"],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "delegated",
                            [A2AOAuthFlowType.DeviceCode],
                            "entra-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("entra", binding.ProviderId);
        Assert.Equal("delegated", binding.RegistrationId);
        Assert.Equal(OAuthCredentialProviderType.Entra, binding.ProviderType);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode"),
            binding.DeviceAuthorizationEndpoint);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations/oauth2/v2.0/token"),
            binding.TokenEndpoint);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations/v2.0/.well-known/openid-configuration"),
            binding.MetadataUrl);
        Assert.Equal(["api://agent/access_as_user", "offline_access"], binding.EffectiveScopes);
        Assert.Equal("entra", binding.ProviderIdentity);
        Assert.Null(binding.AuthorizationEndpoint);
        Assert.Null(binding.RegistrationEndpoint);
    }

    [Fact]
    public async Task ResolveAsync_GenericOAuthMatchesAllAdvertisedEndpointsOnAllowedOrigin()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "any-name",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read", "repo.write"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    AdditionalScopes = ["offline_access", "repo.read"],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "generic-client-id",
                            ClientSecret: null,
                            RedirectUri: new Uri("http://localhost:8400/callback/"),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("generic", binding.ProviderId);
        Assert.Equal("browser", binding.RegistrationId);
        Assert.Equal(
            new Uri("https://identity.example.com/oauth/authorize"),
            binding.AuthorizationEndpoint);
        Assert.Equal(
            new Uri("https://identity.example.com/oauth/token"),
            binding.TokenEndpoint);
        Assert.Equal(["repo.read", "repo.write", "offline_access"], binding.EffectiveScopes);
        Assert.Null(binding.DeviceAuthorizationEndpoint);
        Assert.Null(binding.MetadataUrl);
        Assert.Null(binding.RegistrationEndpoint);
    }

    [Fact]
    public async Task ResolveAsync_GenericPkceRejectsAuthorizationCodeRegistrationWithoutPkce()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic-pkce",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "pkce-client-id",
                            ClientSecret: null,
                            RedirectUri: new Uri("http://localhost:8400/callback/"),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic-pkce", exception.Message);
        Assert.Contains("PKCE", exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_GenericPkceSkipsRegistrationWithoutRedirectUriAndSelectsValidSibling()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic-pkce",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-redirect",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "missing-redirect-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true),
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "browser-client-id",
                            ClientSecret: null,
                            RedirectUri: new Uri("http://localhost:8400/callback/"),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("browser", binding.RegistrationId);
        Assert.Equal("browser-client-id", binding.Registration.ClientId);
    }

    [Fact]
    public async Task ResolveAsync_GenericPkceRejectsRegistrationWithoutRedirectUri()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic-pkce",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-redirect",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "missing-redirect-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic-pkce", exception.Message);
        Assert.Contains(nameof(A2AOAuthFlowType.AuthorizationCode), exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_GenericPkceRejectsSecretBasedRegistrationWithoutSecret()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2PkceCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic-pkce",
                    Type = OAuthCredentialProviderType.GenericOAuth2Pkce,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-secret",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "missing-secret-client-id",
                            ClientSecret: " ",
                            RedirectUri: new Uri("http://localhost:8400/callback/"),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
                            UsePkce: true)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic-pkce", exception.Message);
        Assert.Contains(nameof(A2AOAuthFlowType.AuthorizationCode), exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_AuthorizationCodeSkipsRegistrationWithoutRedirectUriAndSelectsValidSibling()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-redirect",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "missing-redirect-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false),
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "browser-client-id",
                            ClientSecret: null,
                            RedirectUri: new Uri("http://localhost:8400/callback/"),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("browser", binding.RegistrationId);
        Assert.Equal("browser-client-id", binding.Registration.ClientId);
    }

    [Fact]
    public async Task ResolveAsync_AuthorizationCodeRejectsRegistrationWithoutRedirectUri()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-redirect",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "missing-redirect-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic", exception.Message);
        Assert.Contains(nameof(A2AOAuthFlowType.AuthorizationCode), exception.Message);
    }

    [Theory]
    [InlineData("https://localhost:8400/callback/")]
    [InlineData("http://localhost:8400/callback")]
    public async Task ResolveAsync_AuthorizationCodeRejectsRegistrationWithoutSupportedLoopbackRedirect(
        string redirectUri)
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "browser-oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "browser-client-id",
                            ClientSecret: null,
                            RedirectUri: new Uri(redirectUri),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic", exception.Message);
        Assert.Contains(nameof(A2AOAuthFlowType.AuthorizationCode), exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_ClientCredentialsSelectsCompatibleRegistrationOnly()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "application",
            A2AAuthMode.App,
            new OAuthFlows
            {
                ClientCredentials = new()
                {
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["api://agent/.default"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "delegated",
                            [A2AOAuthFlowType.DeviceCode],
                            "delegated-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true),
                        new OAuthClientRegistration(
                            "application",
                            [A2AOAuthFlowType.ClientCredentials],
                            "application-client-id",
                            ClientSecret: "secret",
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
                            UsePkce: false)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("application", binding.RegistrationId);
        Assert.Equal("application-client-id", binding.Registration.ClientId);
        Assert.Equal(A2AOAuthFlowType.ClientCredentials, binding.FlowType);
    }

    [Fact]
    public async Task ResolveAsync_ClientCredentialsSkipsSecretBasedRegistrationWithoutSecretAndSelectsValidSibling()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "application",
            A2AAuthMode.App,
            new OAuthFlows
            {
                ClientCredentials = new()
                {
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["api://agent/.default"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-secret",
                            [A2AOAuthFlowType.ClientCredentials],
                            "missing-secret-client-id",
                            ClientSecret: " ",
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
                            UsePkce: false),
                        new OAuthClientRegistration(
                            "application",
                            [A2AOAuthFlowType.ClientCredentials],
                            "application-client-id",
                            ClientSecret: "secret",
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
                            UsePkce: false)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("application", binding.RegistrationId);
        Assert.Equal("application-client-id", binding.Registration.ClientId);
    }

    [Fact]
    public async Task ResolveAsync_ClientCredentialsRejectsSecretBasedRegistrationWithoutSecret()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "application",
            A2AAuthMode.App,
            new OAuthFlows
            {
                ClientCredentials = new()
                {
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["api://agent/.default"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "missing-secret",
                            [A2AOAuthFlowType.ClientCredentials],
                            "missing-secret-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.ClientSecretBasic,
                            UsePkce: false)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic", exception.Message);
        Assert.Contains(nameof(A2AOAuthFlowType.ClientCredentials), exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_EntraOutranksGenericOriginMatch()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                },
            },
            ["api://agent/access_as_user"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://login.microsoftonline.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "generic-registration",
                            [A2AOAuthFlowType.DeviceCode],
                            "generic-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }),
            new EntraOAuthCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "entra",
                    Type = OAuthCredentialProviderType.Entra,
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "entra-registration",
                            [A2AOAuthFlowType.DeviceCode],
                            "entra-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("entra", binding.ProviderId);
        Assert.Equal("entra-registration", binding.RegistrationId);
    }

    [Fact]
    public async Task ResolveAsync_PathSpecificAuthorityOutranksHostWideAuthority()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "entra",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/tenant-a/oauth2/v2.0/devicecode",
                    TokenUrl = "https://login.microsoftonline.com/tenant-a/oauth2/v2.0/token",
                },
            },
            ["api://agent/access_as_user"]);
        var resolver = CreateResolver(
            new EntraOAuthCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "host-wide",
                    Type = OAuthCredentialProviderType.Entra,
                    AllowedAuthorities = [new Uri("https://login.microsoftonline.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "host",
                            [A2AOAuthFlowType.DeviceCode],
                            "host-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }),
            new EntraOAuthCredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "tenant-a",
                    Type = OAuthCredentialProviderType.Entra,
                    AllowedAuthorities = [new Uri("https://login.microsoftonline.com/tenant-a")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "tenant",
                            [A2AOAuthFlowType.DeviceCode],
                            "tenant-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("tenant-a", binding.ProviderId);
        Assert.Equal("tenant", binding.RegistrationId);
    }

    [Fact]
    public async Task ResolveAsync_EqualRankedProvidersThrowAndListProviderIds()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://identity.example.com/oauth/device",
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic-one",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "first",
                            [A2AOAuthFlowType.DeviceCode],
                            "first-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }),
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic-two",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "second",
                            [A2AOAuthFlowType.DeviceCode],
                            "second-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic-one/first", exception.Message);
        Assert.Contains("generic-two/second", exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_MixedOriginEndpointsAreRejected()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://identity.example.com/oauth/authorize",
                    TokenUrl = "https://malicious.example.com/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "browser",
                            [A2AOAuthFlowType.AuthorizationCode],
                            "client-id",
                            ClientSecret: null,
                            RedirectUri: new Uri("http://localhost:8400/callback/"),
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: false)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("token endpoint", exception.Message);
        Assert.Contains("malicious.example.com", exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_NoCompatibleRegistrationReportsProviderIdAndRequiredFlow()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "application",
            A2AAuthMode.App,
            new OAuthFlows
            {
                ClientCredentials = new()
                {
                    TokenUrl = "https://identity.example.com/oauth/token",
                },
            },
            ["api://agent/.default"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedOrigins = [new Uri("https://identity.example.com")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "delegated",
                            [A2AOAuthFlowType.DeviceCode],
                            "delegated-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("generic", exception.Message);
        Assert.Contains(nameof(A2AOAuthFlowType.ClientCredentials), exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_AuthorityPathMatchingIsCaseSensitive()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://identity.example.com/tenant/oauth/device",
                    TokenUrl = "https://identity.example.com/tenant/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedAuthorities = [new Uri("https://identity.example.com/Tenant")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "device",
                            [A2AOAuthFlowType.DeviceCode],
                            "device-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(s_agentOrigin, authentication, CancellationToken.None));

        Assert.Contains("No OAuth provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_AuthorityHostMatchingRemainsCaseInsensitive()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(
            "oauth",
            A2AAuthMode.Delegated,
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://Identity.Example.com/tenant/oauth/device",
                    TokenUrl = "https://identity.example.COM/tenant/oauth/token",
                },
            },
            ["repo.read"]);
        var resolver = CreateResolver(
            new GenericOAuth2CredentialProvider(
                new OAuthCredentialProviderOptions
                {
                    Id = "generic",
                    Type = OAuthCredentialProviderType.GenericOAuth2,
                    AllowedAuthorities = [new Uri("https://identity.example.com/tenant")],
                    Registrations = CreateRegistrations(
                        new OAuthClientRegistration(
                            "device",
                            [A2AOAuthFlowType.DeviceCode],
                            "device-client-id",
                            ClientSecret: null,
                            RedirectUri: null,
                            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                            UsePkce: true)),
                }));

        OAuthCredentialBinding binding = await resolver.ResolveAsync(
            s_agentOrigin,
            authentication,
            CancellationToken.None);

        Assert.Equal("generic", binding.ProviderId);
        Assert.Equal("device", binding.RegistrationId);
    }

    private static OAuthCredentialProviderResolver CreateResolver(params IOAuthCredentialProvider[] providers)
        => new(providers);

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

    private static A2AAgentCardAuthentication CreateAuthentication(
        string securitySchemeName,
        A2AAuthMode mode,
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

        return A2AAgentCardAuthentication.Select(card, mode);
    }
}
