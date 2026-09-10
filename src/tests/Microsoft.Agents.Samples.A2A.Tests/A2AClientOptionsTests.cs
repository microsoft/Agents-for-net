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
                ["Authentication:TenantId"] = "tenant-id",
            })
            .Build();
        var startupOptions = new StartupOptions
        {
            AgentUrl = new Uri("https://command-line.example/a2a"),
        };

        A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, startupOptions);

        Assert.Equal(new Uri("https://command-line.example/a2a"), options.AgentUrl);
        Assert.Equal("tenant-id", options.Authentication.TenantId);
    }
}
