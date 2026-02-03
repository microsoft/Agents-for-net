// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
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
    /// An A2A Skill definition.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="tags">Delimited with space, comma, semi-colon</param>
    /// <param name="description"></param>
    /// <param name="examples">Semicolon delimited list of examples.</param>
    /// <param name="inputModes">Supported media types for input. Delimited with space, comma, semi-colon</param>
    /// <param name="outputModes">Supported media types for output. Delimited with space, comma, semi-colon</param>
    public A2ASkillAttribute(string name, string tags, string id = null, string description = null, string examples = null, string inputModes = null, string outputModes = null)
    {
        AssertionHelpers.ThrowIfNullOrEmpty(name ?? id, nameof(name));
        AssertionHelpers.ThrowIfNullOrEmpty(tags, nameof(tags));

        Name = name;
        Id = id ?? Name;
        Description = description ?? Name;

#if !NETSTANDARD
        Tags = tags.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        Examples = !string.IsNullOrEmpty(examples) ? examples.Split([';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        InputModes = !string.IsNullOrEmpty(inputModes) ? inputModes.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        OutputModes = !string.IsNullOrEmpty(outputModes) ? outputModes.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : null;
#else
        Tags = tags.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
        Examples = !string.IsNullOrEmpty(examples) ? examples.Split([';'], StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        InputModes = !string.IsNullOrEmpty(inputModes) ? inputModes.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).ToList() : null;
        OutputModes = !string.IsNullOrEmpty(outputModes) ? outputModes.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).ToList() : null;
#endif
    }
};
