// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Provides helpers for converting metadata and composing A2A agent messages.
/// </summary>
public static class A2AExtensions
{
    private static readonly ConcurrentDictionary<string, JsonNode> _schemas = new();
    private static readonly JsonSchemaExporterOptions _exporterOptions = new() { TreatNullObliviousAsNonNullable = true };
    private static readonly JsonSerializerOptions _reflectionOptions = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(AIJsonUtilities.DefaultOptions.TypeInfoResolver, new DefaultJsonTypeInfoResolver())
    };

    /// <summary>
    /// Creates A2A metadata that describes the JSON schema of a value.
    /// </summary>
    /// <param name="data">The value whose runtime type is described.</param>
    /// <param name="contentType">The media type of the value.</param>
    /// <returns>A metadata dictionary containing the content type and generated JSON schema.</returns>
    public static Dictionary<string, JsonElement> ToA2AMetadata(this object data, string contentType)
    {
        if (!_schemas.TryGetValue(data.GetType().FullName, out JsonNode schema))
        {
            schema = _reflectionOptions.GetJsonSchemaAsNode(data.GetType(), _exporterOptions);
            _schemas[data.GetType().FullName] = schema;
        }

        return new Dictionary<string, JsonElement>
        {
            ["mimeType"] = JsonSerializer.SerializeToElement(contentType),
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["schema"] = JsonSerializer.SerializeToElement(schema, _reflectionOptions)
        };
    }

    /// <summary>
    /// Creates an agent-role message associated with a task update.
    /// </summary>
    /// <param name="updater">The task update that provides the task and context identifiers.</param>
    /// <param name="text">The text content for the message.</param>
    /// <returns>A message containing a single text part.</returns>
    public static Message NewAgentMessage(this TaskUpdater updater, string text)
    {
        return new Message()
        {
            Role = Role.Agent,
            TaskId = updater.TaskId,
            ContextId = updater.ContextId,
            Parts = [new Part() { Text = text }]
        };
    }

    /// <summary>
    /// Creates an agent-role message associated with a task update.
    /// </summary>
    /// <param name="updater">The task update that provides the task and context identifiers.</param>
    /// <param name="parts">The message parts to send.</param>
    /// <returns>A message containing <paramref name="parts"/>.</returns>
    public static Message NewAgentMessage(this TaskUpdater updater, List<Part> parts)
    {
        return new Message()
        {
            Role = Role.Agent,
            TaskId = updater.TaskId,
            ContextId = updater.ContextId,
            Parts = parts
        };
    }
}