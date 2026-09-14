// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Builder.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.A2A;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
[RouteHandlerType(typeof(A2ARouteHandler))]
public class A2ASkillAttribute : Attribute
{
    /// <summary>
    /// Unique identifier for the agent's skill.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Human readable name of the skill.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description of the skill.
    /// </summary>
    /// <remarks>
    /// Will be used by the client or a human as a hint to understand what the skill does.
    /// </remarks>
    public string Description { get; }

    /// <summary>
    /// Set of tagwords describing classes of capabilities for this specific skill.
    /// </summary>
    public List<string> Tags { get; }

    /// <summary>
    /// The set of example scenarios that the skill can perform.
    /// </summary>
    /// <remarks>
    /// Will be used by the client as a hint to understand how the skill can be used.
    /// </remarks>
    [JsonPropertyName("examples")]
    public List<string>? Examples { get; set; }

    /// <summary>
    /// The set of interaction modes that the skill supports (if different than the default).
    /// </summary>
    /// <remarks>
    /// Supported media types for input.
    /// </remarks>
    public List<string>? InputModes { get; set; }

    /// <summary>
    /// Supported media types for output.
    /// </summary>
    public List<string>? OutputModes { get; set; }

    /// <summary>
    /// The exact A2A message text that invokes the skill.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The regular expression that selects A2A messages invoking the skill.
    /// </summary>
    public string TextRegex { get; }

    /// <summary>
    /// Whether the skill route accepts only agentic requests.
    /// </summary>
    public bool IsAgenticOnly { get; }

    /// <summary>
    /// The static authorization handlers required by this skill route.
    /// </summary>
    public string[] AutoSignInHandlers { get; }

    /// <summary>
    /// An A2A Skill definition.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="tags">Delimited with space, comma, semi-colon</param>
    /// <param name="description"></param>
    /// <param name="examples">Semicolon delimited list of examples.</param>
    /// <param name="inputModes">Supported media types for input. Delimited with space, comma, semi-colon</param>
    /// <param name="outputModes">Supported media types for output. Delimited with space, comma, semi-colon</param>
    /// <param name="text">The exact message text that invokes the skill.</param>
    /// <param name="textRegex">A regular expression that selects messages invoking the skill.</param>
    /// <param name="isAgenticOnly">Whether the skill route accepts only agentic requests.</param>
    /// <param name="rank">The route evaluation order.</param>
    /// <param name="autoSigninHandlers">Delimited static authorization handler names.</param>
    public A2ASkillAttribute(
        string name,
        string tags,
        string id = null,
        string description = null,
        string examples = null,
        string inputModes = null,
        string outputModes = null,
        string text = null,
        string textRegex = null,
        bool isAgenticOnly = false,
        ushort rank = RouteRank.Unspecified,
        string autoSigninHandlers = null)
    {
        AssertionHelpers.ThrowIfNullOrEmpty(name ?? id, nameof(name));
        AssertionHelpers.ThrowIfNullOrEmpty(tags, nameof(tags));
        if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(textRegex))
        {
            throw new ArgumentException("A skill route cannot specify both text and textRegex.");
        }

        Name = name;
        Id = id ?? Name;
        Description = description ?? Name;

        Tags = tags.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        Examples = !string.IsNullOrEmpty(examples) ? examples.Split([';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        InputModes = !string.IsNullOrEmpty(inputModes) ? inputModes.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        OutputModes = !string.IsNullOrEmpty(outputModes) ? outputModes.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        Text = text;
        TextRegex = textRegex;
        IsAgenticOnly = isAgenticOnly;
        Rank = rank;
        AutoSignInHandlers = RouteAttributeHelper.DelimitedToList(autoSigninHandlers) ?? [];
    }

    /// <summary>
    /// The route evaluation order.
    /// </summary>
    public ushort Rank { get; }

    internal A2ASkillBuilder CreateBuilder(MethodInfo method, AgentApplication app)
    {
        var handler = (A2ARouteHandler)RouteAttributeHelper.CreateMatchingHandlerDelegate(app, method, GetType());
        var builder = new A2ASkillBuilder(Id)
            .WithName(Name)
            .WithDescription(Description)
            .WithTags(Tags.ToArray())
            .WithExamples(Examples?.ToArray() ?? [])
            .WithInputModes(InputModes?.ToArray() ?? [])
            .WithOutputModes(OutputModes?.ToArray() ?? []);

        if (!string.IsNullOrWhiteSpace(Text))
        {
            return builder.OnMessage(Text, handler, AutoSignInHandlers, Rank, IsAgenticOnly);
        }

        if (!string.IsNullOrWhiteSpace(TextRegex))
        {
            return builder.OnMessage(new System.Text.RegularExpressions.Regex(TextRegex), handler, AutoSignInHandlers, Rank, IsAgenticOnly);
        }

        return builder.OnMessage(handler, AutoSignInHandlers, Rank, IsAgenticOnly);
    }
}
