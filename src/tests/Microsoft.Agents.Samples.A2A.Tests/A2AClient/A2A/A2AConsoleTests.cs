// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using A2A;
using Microsoft.Agents.Samples.A2AClient.A2A;
using Moq;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests.A2A;

public class A2AConsoleTests
{
    [Theory]
    [InlineData(":auth none", 0)]
    [InlineData(":auth delegated", 1)]
    [InlineData(":auth app", 2)]
    public void TryHandleCommand_AuthCommand_ChangesMode(string command, int expectedValue)
    {
        var session = new A2AAuthenticationSession();
        var console = CreateConsole(session);

        bool handled = console.TryHandleCommand(command);

        Assert.True(handled);
        Assert.Equal((A2AAuthMode)expectedValue, session.Mode);
    }

    [Fact]
    public void TryHandleCommand_AuthAuto_ClearsModeOverride()
    {
        var session = new A2AAuthenticationSession();
        session.SetMode(A2AAuthMode.Delegated);
        var console = CreateConsole(session);

        bool handled = console.TryHandleCommand(":auth auto");

        Assert.True(handled);
        Assert.Null(session.ModeOverride);
    }

    [Fact]
    public void TryHandleCommand_ExplicitAuthModeAfterAutomaticSelection_ClearsSelectedAuthentication()
    {
        var session = new A2AAuthenticationSession();
        session.SetAutomaticAuthentication(CreateDelegatedAuthentication());
        var console = CreateConsole(session);

        bool handled = console.TryHandleCommand(":auth delegated");

        Assert.True(handled);
        Assert.Equal(A2AAuthMode.Delegated, session.Mode);
        Assert.Equal(A2AAuthMode.Delegated, session.ModeOverride);
        Assert.Null(session.SelectedAuthentication);
    }

    [Theory]
    [InlineData(":q")]
    [InlineData("quit")]
    public void TryHandleCommand_QuitCommand_StopsLoop(string command)
    {
        var console = CreateConsole(new A2AAuthenticationSession());

        bool handled = console.TryHandleCommand(command);

        Assert.True(handled);
        Assert.False(console.IsRunning);
    }

    [Fact]
    public void TryHandleCommand_OrdinaryText_IsNotHandled()
    {
        var console = CreateConsole(new A2AAuthenticationSession());

        bool handled = console.TryHandleCommand("Tell me about A2A.");

        Assert.False(handled);
        Assert.True(console.IsRunning);
    }

    [Theory]
    [InlineData(":history on", true)]
    [InlineData(":history off", false)]
    public void TryHandleCommand_HistoryCommand_ChangesHistorySetting(string command, bool expected)
    {
        var console = CreateConsole(new A2AAuthenticationSession());

        bool handled = console.TryHandleCommand(command);

        Assert.True(handled);
        Assert.Equal(expected, console.ShowHistory);
    }

    private static A2AConsole CreateConsole(A2AAuthenticationSession session)
    {
        return new A2AConsole(
            new Mock<IA2AClient>(MockBehavior.Strict).Object,
            new AgentCard(),
            session,
            TextReader.Null,
            TextWriter.Null,
            showHistory: false,
            usePushNotifications: false,
            new Uri("http://localhost:5000"));
    }

    private static A2AAgentCardAuthentication CreateDelegatedAuthentication()
    {
        AgentCard card = new()
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                            },
                        },
                    },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }
}
