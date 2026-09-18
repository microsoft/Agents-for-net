// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using A2A;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class A2AAgentCardSkillSelector
{
    private static readonly HashSet<string> s_ignoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "for", "give", "in", "is", "me", "of", "please", "sample",
        "tell", "the", "to", "what", "you",
    };

    public static A2AAgentCardSkillSelection Select(AgentCard card, string input)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        string normalizedInput = input.Trim();
        AgentSkill[] exactMatches = (card.Skills ?? []).Where(candidate =>
            (candidate.Examples ?? []).Any(example =>
                string.Equals(example?.Trim(), normalizedInput, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (exactMatches.Length == 1)
        {
            return new A2AAgentCardSkillSelection(exactMatches[0], []);
        }

        if (exactMatches.Length > 1)
        {
            return new A2AAgentCardSkillSelection(null, exactMatches);
        }

        HashSet<string> inputTerms = GetTerms(normalizedInput);
        if (inputTerms.Count == 0)
        {
            return new A2AAgentCardSkillSelection(null, []);
        }

        var scores = (card.Skills ?? [])
            .Select(skill => new
            {
                Skill = skill,
                Score =
                    (4 * CountMatches(inputTerms, skill.Name))
                    + (3 * CountMatches(inputTerms, skill.Tags))
                    + (2 * CountMatches(inputTerms, skill.Examples))
                    + CountMatches(inputTerms, skill.Description),
            })
            .Where(candidate => candidate.Score > 0)
            .ToArray();
        if (scores.Length == 0)
        {
            return new A2AAgentCardSkillSelection(null, []);
        }

        int highestScore = scores.Max(candidate => candidate.Score);
        AgentSkill[] bestMatches = scores
            .Where(candidate => candidate.Score == highestScore)
            .Select(candidate => candidate.Skill)
            .ToArray();

        return bestMatches.Length == 1
            ? new A2AAgentCardSkillSelection(bestMatches[0], [])
            : new A2AAgentCardSkillSelection(null, bestMatches);
    }

    private static int CountMatches(HashSet<string> inputTerms, string? value)
        => GetTerms(value).Count(inputTerms.Contains);

    private static int CountMatches(HashSet<string> inputTerms, IEnumerable<string>? values)
        => (values ?? []).SelectMany(GetTerms).Distinct(StringComparer.OrdinalIgnoreCase).Count(inputTerms.Contains);

    private static HashSet<string> GetTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return Regex.Matches(value, "[A-Za-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 1 && !s_ignoredTerms.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed class A2AAgentCardSkillSelection(
    AgentSkill? skill,
    IReadOnlyList<AgentSkill> ambiguousSkills)
{
    public AgentSkill? Skill { get; } = skill;

    public IReadOnlyList<AgentSkill> AmbiguousSkills { get; } = ambiguousSkills;

    public bool IsAmbiguous => AmbiguousSkills.Count > 0;
}
