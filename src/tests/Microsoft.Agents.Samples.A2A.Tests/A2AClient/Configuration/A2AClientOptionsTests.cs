// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Agents.Samples.A2AClient;
using Microsoft.Agents.Samples.A2AClient.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests.Configuration;

public class A2AClientOptionsTests
{
    [Fact]
    public void ParseStartupOptions_AllSupportedOptions_AreParsed()
    {
        StartupOptions options = Program.ParseStartupOptions(
        [
            "--agent", "https://agent.example/a2a",
            "--history",
            "--use-push-notifications",
            "--push-notification-receiver", "https://receiver.example",
            "--auth-mode", "delegated",
        ]);

        Assert.Equal(new Uri("https://agent.example/a2a"), options.AgentUrl);
        Assert.True(options.ShowHistory);
        Assert.True(options.UsePushNotifications);
        Assert.Equal(new Uri("https://receiver.example"), options.PushNotificationReceiver);
        Assert.Equal(A2AAuthMode.Delegated, options.AuthMode);
    }

    [Fact]
    public void ParseStartupOptions_WithoutAuthMode_LeavesAutomaticSelectionEnabled()
    {
        StartupOptions options = Program.ParseStartupOptions([]);

        Assert.Null(options.AuthMode);
    }

    [Fact]
    public void FromConfiguration_MissingAgentUrl_NamesMissingKey()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("A2A:AgentUrl", exception.Message);
    }

    [Fact]
    public void FromConfiguration_CommandLineAgent_OverridesConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A2A:AgentUrl"] = "https://configured.example/a2a",
            })
            .Build();
        var startupOptions = new StartupOptions
        {
            AgentUrl = new Uri("https://command-line.example/a2a"),
        };

        A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, startupOptions);

        Assert.Equal(new Uri("https://command-line.example/a2a"), options.AgentUrl);
        Assert.Empty(options.Authentication.Providers);
    }

    [Fact]
    public void FromConfiguration_BindsConfiguredOAuthProvidersAndRegistrations()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A2A:AgentUrl"] = "https://agent.example/a2a",
                ["Authentication:Providers:entra:Type"] = "Entra",
                ["Authentication:Providers:entra:AllowedAuthorities:0"] = "https://login.microsoftonline.com",
                ["Authentication:Providers:entra:AdditionalScopes:0"] = "offline_access",
                ["Authentication:Providers:entra:Registrations:delegated:GrantTypes:0"] = "DeviceCode",
                ["Authentication:Providers:entra:Registrations:delegated:GrantTypes:1"] = "AuthorizationCode",
                ["Authentication:Providers:entra:Registrations:delegated:ClientId"] = "entra-client-id",
                ["Authentication:Providers:entra:Registrations:delegated:RedirectUri"] = "http://localhost:8400/callback/",
                ["Authentication:Providers:dcr:Type"] = "OAuth21PkceDcr",
                ["Authentication:Providers:dcr:AllowInteractiveApproval"] = "true",
                ["Authentication:Providers:dcr:RedirectUri"] = "http://localhost:8400/callback/",
            })
            .Build();

        A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, new StartupOptions());

        OAuthCredentialProviderOptions entra = Assert.IsType<OAuthCredentialProviderOptions>(options.Authentication.Providers["entra"]);
        Assert.Equal(OAuthCredentialProviderType.Entra, entra.Type);
        Assert.Equal([new Uri("https://login.microsoftonline.com")], entra.AllowedAuthorities);
        Assert.Equal(["offline_access"], entra.AdditionalScopes);

        OAuthClientRegistration delegated = Assert.IsType<OAuthClientRegistration>(entra.Registrations["delegated"]);
        Assert.Equal([A2AOAuthFlowType.DeviceCode, A2AOAuthFlowType.AuthorizationCode], delegated.GrantTypes);
        Assert.Equal("entra-client-id", delegated.ClientId);
        Assert.Equal(new Uri("http://localhost:8400/callback/"), delegated.RedirectUri);

        OAuthCredentialProviderOptions dcr = Assert.IsType<OAuthCredentialProviderOptions>(options.Authentication.Providers["dcr"]);
        Assert.Equal(OAuthCredentialProviderType.OAuth21PkceDcr, dcr.Type);
        Assert.True(dcr.AllowInteractiveApproval);
        Assert.Equal(new Uri("http://localhost:8400/callback/"), dcr.RedirectUri);
    }

    [Fact]
    public void FromConfiguration_SampleAppSettings_BindsDocumentedProviderCatalog()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(GetA2AClientSampleAppSettingsPath(), optional: false)
            .Build();

        A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, new StartupOptions());

        OAuthCredentialProviderOptions entra = Assert.IsType<OAuthCredentialProviderOptions>(options.Authentication.Providers["entra"]);
        Assert.Equal(OAuthCredentialProviderType.Entra, entra.Type);
        Assert.Equal(["offline_access"], entra.AdditionalScopes);
        Assert.Contains(new Uri("https://login.microsoftonline.com"), entra.AllowedAuthorities);
        Assert.Equal(
            [A2AOAuthFlowType.DeviceCode, A2AOAuthFlowType.AuthorizationCode],
            entra.Registrations["delegated"].GrantTypes);
        Assert.Equal([A2AOAuthFlowType.ClientCredentials], entra.Registrations["application"].GrantTypes);
        Assert.Equal(
            OAuthTokenEndpointAuthenticationMethod.ClientSecretPost,
            entra.Registrations["application"].TokenEndpointAuthenticationMethod);
        Assert.False(string.IsNullOrWhiteSpace(entra.Registrations["application"].ClientSecret));

        OAuthCredentialProviderOptions genericPkce = Assert.IsType<OAuthCredentialProviderOptions>(options.Authentication.Providers["browser-oauth"]);
        Assert.Equal(OAuthCredentialProviderType.GenericOAuth2Pkce, genericPkce.Type);
        Assert.Contains(new Uri("https://identity.example.com"), genericPkce.AllowedOrigins);
        Assert.Equal(
            [A2AOAuthFlowType.AuthorizationCode],
            genericPkce.Registrations["browser"].GrantTypes);

        OAuthCredentialProviderOptions dcr = Assert.IsType<OAuthCredentialProviderOptions>(options.Authentication.Providers["interactive-dcr"]);
        Assert.Equal(OAuthCredentialProviderType.OAuth21PkceDcr, dcr.Type);
        Assert.True(dcr.AllowInteractiveApproval);
        Assert.Equal(new Uri("http://localhost:8402/callback/"), dcr.RedirectUri);
        Assert.Empty(dcr.Registrations);
    }

    [Fact]
    public void FromConfiguration_UnknownProviderType_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:generic:Type", "NotAProvider"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("Authentication:Providers:generic:Type", exception.Message);
    }

    [Fact]
    public void FromConfiguration_RelativeAllowedAuthority_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:entra:Type", "Entra"),
            ("Authentication:Providers:entra:AllowedAuthorities:0", "/tenant"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("Authentication:Providers:entra:AllowedAuthorities:0", exception.Message);
    }

    [Fact]
    public void FromConfiguration_DuplicateGrantType_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:entra:Type", "Entra"),
            ("Authentication:Providers:entra:Registrations:delegated:GrantTypes:0", "AuthorizationCode"),
            ("Authentication:Providers:entra:Registrations:delegated:GrantTypes:1", "AuthorizationCode"),
            ("Authentication:Providers:entra:Registrations:delegated:ClientId", "entra-client-id"),
            ("Authentication:Providers:entra:Registrations:delegated:RedirectUri", "http://localhost:8400/callback/"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("GrantTypes", exception.Message);
    }

    [Fact]
    public void FromConfiguration_BlankConfiguredClientId_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:entra:Type", "Entra"),
            ("Authentication:Providers:entra:Registrations:delegated:GrantTypes:0", "AuthorizationCode"),
            ("Authentication:Providers:entra:Registrations:delegated:ClientId", " "));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("ClientId", exception.Message);
    }

    [Fact]
    public void FromConfiguration_RegistrationWithoutGrantTypes_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:entra:Type", "Entra"),
            ("Authentication:Providers:entra:Registrations:delegated:ClientId", "entra-client-id"),
            ("Authentication:Providers:entra:Registrations:delegated:RedirectUri", "http://localhost:8400/callback/"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:entra:Registrations:delegated:GrantTypes",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://identity.example.com/callback/")]
    [InlineData("http://localhost:8400/callback")]
    [InlineData("http://contoso.example/callback/")]
    [InlineData("/callback/")]
    public void FromConfiguration_UnsupportedProviderRedirectUri_Throws(string redirectUri)
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:dcr:Type", "OAuth21PkceDcr"),
            ("Authentication:Providers:dcr:AllowInteractiveApproval", "true"),
            ("Authentication:Providers:dcr:RedirectUri", redirectUri));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("Authentication:Providers:dcr:RedirectUri", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_UnsupportedRegistrationRedirectUri_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:entra:Type", "Entra"),
            ("Authentication:Providers:entra:Registrations:delegated:GrantTypes:0", "AuthorizationCode"),
            ("Authentication:Providers:entra:Registrations:delegated:ClientId", "entra-client-id"),
            ("Authentication:Providers:entra:Registrations:delegated:RedirectUri", "https://identity.example.com/callback/"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:entra:Registrations:delegated:RedirectUri",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_DcrProviderWithoutRedirectUri_ThrowsEvenWithoutInteractiveApproval()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:dcr:Type", "OAuth21PkceDcr"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains("Authentication:Providers:dcr:RedirectUri", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_MalformedAllowInteractiveApproval_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:dcr:Type", "OAuth21PkceDcr"),
            ("Authentication:Providers:dcr:AllowInteractiveApproval", "yes"),
            ("Authentication:Providers:dcr:RedirectUri", "http://localhost:8400/callback/"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:dcr:AllowInteractiveApproval",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_MalformedUsePkce_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:generic:Type", "GenericOAuth2"),
            ("Authentication:Providers:generic:AllowedOrigins:0", "https://identity.example.com"),
            ("Authentication:Providers:generic:Registrations:browser:GrantTypes:0", "AuthorizationCode"),
            ("Authentication:Providers:generic:Registrations:browser:ClientId", "browser-client-id"),
            ("Authentication:Providers:generic:Registrations:browser:RedirectUri", "http://localhost:8400/callback/"),
            ("Authentication:Providers:generic:Registrations:browser:UsePkce", "sometimes"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:generic:Registrations:browser:UsePkce",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_NoneAuthenticationWithClientSecret_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:generic:Type", "GenericOAuth2"),
            ("Authentication:Providers:generic:AllowedOrigins:0", "https://identity.example.com"),
            ("Authentication:Providers:generic:Registrations:browser:GrantTypes:0", "AuthorizationCode"),
            ("Authentication:Providers:generic:Registrations:browser:ClientId", "browser-client-id"),
            ("Authentication:Providers:generic:Registrations:browser:RedirectUri", "http://localhost:8400/callback/"),
            ("Authentication:Providers:generic:Registrations:browser:ClientSecret", "browser-secret"),
            ("Authentication:Providers:generic:Registrations:browser:TokenEndpointAuthenticationMethod", "None"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:generic:Registrations:browser",
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("browser-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_ClientCredentialsWithNoneAuthentication_Throws()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:generic:Type", "GenericOAuth2"),
            ("Authentication:Providers:generic:AllowedOrigins:0", "https://identity.example.com"),
            ("Authentication:Providers:generic:Registrations:application:GrantTypes:0", "ClientCredentials"),
            ("Authentication:Providers:generic:Registrations:application:ClientId", "application-client-id"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:generic:Registrations:application",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(nameof(A2AOAuthFlowType.ClientCredentials), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromConfiguration_SecretBasedAuthenticationWithoutSecret_Throws(string? clientSecret)
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Providers:generic:Type", "GenericOAuth2"),
            ("Authentication:Providers:generic:AllowedOrigins:0", "https://identity.example.com"),
            ("Authentication:Providers:generic:Registrations:application:GrantTypes:0", "ClientCredentials"),
            ("Authentication:Providers:generic:Registrations:application:ClientId", "application-client-id"),
            ("Authentication:Providers:generic:Registrations:application:ClientSecret", clientSecret),
            ("Authentication:Providers:generic:Registrations:application:TokenEndpointAuthenticationMethod", "ClientSecretPost"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AClientOptions.FromConfiguration(configuration, new StartupOptions()));

        Assert.Contains(
            "Authentication:Providers:generic:Registrations:application:ClientSecret",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_ConnectionsOnly_DoesNotPopulateProviders()
    {
        IConfiguration configuration = CreateAuthenticationConfiguration(
            ("Authentication:Connections:github:ClientId", "github-client-id"),
            ("Authentication:Connections:github:AllowedOrigins:0", "https://github.com"));

        A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, new StartupOptions());

        Assert.Empty(options.Authentication.Providers);
    }

    private static IConfiguration CreateAuthenticationConfiguration(params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["A2A:AgentUrl"] = "https://agent.example/a2a",
        };

        foreach ((string key, string? value) in settings)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string GetA2AClientSampleAppSettingsPath()
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "src",
            "samples",
            "A2A",
            "A2AClient",
            "appsettings.json"));
}
