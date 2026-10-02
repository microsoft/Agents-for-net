// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Samples.A2AClient.A2A;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests.A2A;

public class A2AAgentCardSkillSelectorTests
{
    [Fact]
    public void Select_ExactAdvertisedProtectedSkillExample_SelectsMatchingSkill()
    {
        var issues = new AgentSkill
        {
            Id = "issues",
            Name = "GitHub assigned issues",
            Description = "Reads the signed-in GitHub user's open assigned issues.",
            Examples = ["-issues"],
        };
        var card = new AgentCard { Skills = [issues] };

        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(card, "  -ISSUES ");

        Assert.Same(issues, selection.Skill);
        Assert.False(selection.IsAmbiguous);
    }

    [Fact]
    public void Select_TermsMatchOneSkill_SelectsHighestScoringSkill()
    {
        var issues = new AgentSkill
        {
            Id = "issues",
            Name = "GitHub assigned issues",
            Description = "Reads the signed-in GitHub user's open assigned issues.",
            Tags = ["github", "issues"],
            Examples = ["-issues"],
        };
        var card = new AgentCard
        {
            Skills =
            [
                issues,
                new AgentSkill
                {
                    Id = "profile",
                    Name = "Microsoft Graph profile",
                    Description = "Reads the signed-in user's Microsoft Graph profile.",
                    Tags = ["graph", "profile"],
                    Examples = ["-me"],
                },
            ],
        };

        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(
            card,
            "Show my assigned GitHub issues.");

        Assert.Same(issues, selection.Skill);
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
