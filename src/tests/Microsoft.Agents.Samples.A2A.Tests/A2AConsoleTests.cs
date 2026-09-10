// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Moq;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

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
}
