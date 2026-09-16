// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AAgentCardSkillSelectorTests
{
    [Fact]
    public void Select_ExactAdvertisedExample_SelectsMatchingSkill()
    {
        var profile = new AgentSkill
        {
            Id = "profile",
            Name = "Microsoft Graph profile",
            Description = "Reads the delegated caller profile.",
            Examples = ["-me"],
        };
        var card = new AgentCard
        {
            Skills =
            [
                new AgentSkill
                {
                    Id = "stream",
                    Name = "Streaming response",
                    Description = "Streams a response.",
                    Examples = ["-stream"],
                },
                profile,
            ],
        };

        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(card, "  -ME ");

        Assert.Same(profile, selection.Skill);
        Assert.False(selection.IsAmbiguous);
    }

    [Fact]
    public void Select_TermsMatchOneSkill_SelectsHighestScoringSkill()
    {
        var weather = new AgentSkill
        {
            Id = "weather",
            Name = "Weather forecast",
            Description = "Gets the weather for a city.",
            Tags = ["weather", "forecast"],
            Examples = ["What is the weather in Seattle?"],
        };
        var card = new AgentCard
        {
            Skills =
            [
                weather,
                new AgentSkill
                {
                    Id = "profile",
                    Name = "User profile",
                    Description = "Reads the signed-in user's profile.",
                    Tags = ["profile"],
                },
            ],
        };

        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(
            card,
            "Could you give me the Seattle weather forecast?");

        Assert.Same(weather, selection.Skill);
        Assert.False(selection.IsAmbiguous);
    }

    [Fact]
    public void Select_EqualBestScores_ReturnsAmbiguousCandidates()
    {
        var first = new AgentSkill
        {
            Id = "first",
            Name = "Weather",
            Description = "Gets weather.",
        };
        var second = new AgentSkill
        {
            Id = "second",
            Name = "Weather",
            Description = "Gets weather.",
        };
        var card = new AgentCard { Skills = [first, second] };

        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(card, "weather");

        Assert.Null(selection.Skill);
        Assert.Equal([first, second], selection.AmbiguousSkills);
        Assert.True(selection.IsAmbiguous);
    }

    [Fact]
    public void Select_NoMetadataTermsMatch_ReturnsNoSkill()
    {
        var card = new AgentCard
        {
            Skills =
            [
                new AgentSkill
                {
                    Id = "profile",
                    Name = "User profile",
                    Description = "Reads the signed-in user's profile.",
                },
            ],
        };

        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(card, "Tell me a joke.");

        Assert.Null(selection.Skill);
        Assert.Empty(selection.AmbiguousSkills);
        Assert.False(selection.IsAmbiguous);
    }
}
