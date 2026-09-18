// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Samples.A2AClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

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
        Assert.Empty(options.Authentication.Connections);
    }

    [Fact]
    public void FromConfiguration_ReadsOAuthConnectionsBySecuritySchemeName()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A2A:AgentUrl"] = "https://agent.example/a2a",
                ["Authentication:Connections:github:ClientId"] = "github-client-id",
                ["Authentication:Connections:github:AllowedOrigins:0"] = "https://github.com",
                ["Authentication:Connections:github:AdditionalScopes:0"] = "offline_access",
                ["Authentication:Connections:linkedin:ClientId"] = "linkedin-client-id",
                ["Authentication:Connections:linkedin:ClientSecret"] = "linkedin-client-secret",
                ["Authentication:Connections:linkedin:RedirectUri"] = "http://localhost:8400/callback/",
                ["Authentication:Connections:linkedin:AllowedOrigins:0"] = "https://www.linkedin.com",
                ["Authentication:Connections:linkedin:TokenEndpointAuthenticationMethod"] = "ClientSecretPost",
            })
            .Build();

        A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, new StartupOptions());

        OAuthConnectionOptions github = options.Authentication.GetRequiredConnection("github");
        Assert.Equal("github-client-id", github.ClientId);
        Assert.Equal([new Uri("https://github.com")], github.AllowedOrigins);
        Assert.Equal(["offline_access"], github.AdditionalScopes);
        Assert.Equal(OAuthTokenEndpointAuthenticationMethod.None, github.TokenEndpointAuthenticationMethod);

        OAuthConnectionOptions linkedin = options.Authentication.GetRequiredConnection("linkedin");
        Assert.Equal("linkedin-client-id", linkedin.ClientId);
        Assert.Equal("linkedin-client-secret", linkedin.ClientSecret);
        Assert.Equal(new Uri("http://localhost:8400/callback/"), linkedin.RedirectUri);
        Assert.Equal([new Uri("https://www.linkedin.com")], linkedin.AllowedOrigins);
        Assert.Equal(OAuthTokenEndpointAuthenticationMethod.ClientSecretPost, linkedin.TokenEndpointAuthenticationMethod);
    }
}
