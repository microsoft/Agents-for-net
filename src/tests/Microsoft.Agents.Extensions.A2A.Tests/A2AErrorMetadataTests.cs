// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AErrorMetadataTests
{
    [Fact]
    public void ConflictingSkillMetadata_HasStableErrorMetadata()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new A2AAgentExtension(new ConflictingSkillAttributeApp(new AgentApplicationOptions((IStorage)null))));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100002);
        Assert.Contains("shared-skill", exception.Message);
    }

    [Fact]
    public void Build_WithoutMessageRoute_HasStableErrorMetadata()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new A2ASkillBuilder("missing-route").Build());

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100003);
        Assert.Contains("missing-route", exception.Message);
    }

    [Fact]
    public void OnMessage_WhenRouteAlreadyDefined_HasStableErrorMetadata()
    {
        var skill = new A2ASkillBuilder("one-route")
            .OnMessage((_, _, _) => Task.CompletedTask);

        var exception = Assert.Throws<InvalidOperationException>(
            () => skill.OnMessage((_, _, _) => Task.CompletedTask));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100004);
        Assert.Contains("one-route", exception.Message);
    }

    [Fact]
    public void ResolveAgentTypes_WithoutAgentApplication_HasStableErrorMetadata()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => A2AServiceExtensions.ResolveAgentTypes(typeof(string).Assembly, services));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100005);
        Assert.Contains("No AgentApplication was found", exception.Message);
    }

    [Fact]
    public void ResolveAgentInterfaces_WithoutAttributeForMultipleAgents_HasStableErrorMetadata()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => A2AServiceExtensions.ResolveAgentInterfaces(typeof(UnattributedAgent), 2, "/a2a"));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100006);
        Assert.Contains(typeof(UnattributedAgent).FullName, exception.Message);
    }

    private sealed class UnattributedAgent : AgentApplication
    {
        public UnattributedAgent()
            : base(new AgentApplicationOptions((IStorage)null))
        {
        }
    }

}

internal static class A2AErrorMetadataAssertions
{
    internal static void AssertErrorMetadata(Exception exception, int code)
    {
        Assert.Equal(code, exception.HResult);
        Assert.Equal($"https://aka.ms/M365AgentsErrorCodes/#{code}", exception.HelpLink);
    }
}
