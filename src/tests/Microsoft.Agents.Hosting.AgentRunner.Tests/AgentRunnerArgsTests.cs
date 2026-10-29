// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.AgentRunner.Tests;

public class AgentRunnerArgsTests
{
    [Fact]
    public void Parse_ValidArguments_PreservesPortAndPath()
    {
        var parsed = AgentRunnerArgs.Parse(
            ["--port", "3978", "--agent-assembly", @"C:\agent bundle\agent.dll"], out var errors);

        Assert.Empty(errors);
        Assert.Equal(new AgentRunnerArgs(3978, @"C:\agent bundle\agent.dll"), parsed);
    }

    [Theory]
    [InlineData("--port", "3978")]
    [InlineData("--agent-assembly", "agent.dll")]
    [InlineData("--port", "not-a-number", "--agent-assembly", "agent.dll")]
    [InlineData("--port", "0", "--agent-assembly", "agent.dll")]
    [InlineData("--port", "65536", "--agent-assembly", "agent.dll")]
    [InlineData("--port", "3978", "--agent-assembly", " ")]
    [InlineData("--port", "3978", "--agent-assembly", "agent.dll", "--unexpected")]
    public void Parse_InvalidArguments_ReturnsErrors(params string[] args)
    {
        Assert.Null(AgentRunnerArgs.Parse(args, out var errors));
        Assert.NotEmpty(errors);
    }
}
