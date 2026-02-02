// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

/// <summary>
/// Concerning A2A model helpers for extensions, creation, serialization.
/// </summary>
internal static class A2AExtensions
{
    private static readonly ConcurrentDictionary<string, JsonNode> _schemas = new();

    public static bool IsTerminal(this AgentTask task)
    {
        return task.Status.State == TaskState.Completed
            || task.Status.State == TaskState.Canceled
            || task.Status.State == TaskState.Rejected
            || task.Status.State == TaskState.Failed;
    }

    public static Dictionary<string, JsonElement> ToA2AMetadata(this object data, string contentType)
    {
        if (!_schemas.TryGetValue(data.GetType().FullName, out JsonNode schema))
        {
            JsonSchemaExporterOptions exporterOptions = new()
            {
                TreatNullObliviousAsNonNullable = true,
            };

            JsonSerializerOptions DefaultReflectionOptions = new()
            {
                TypeInfoResolver = JsonTypeInfoResolver.Combine(AIJsonUtilities.DefaultOptions.TypeInfoResolver, new DefaultJsonTypeInfoResolver())
            };

            schema = DefaultReflectionOptions.GetJsonSchemaAsNode(data.GetType(), exporterOptions);
            _schemas[data.GetType().FullName] = schema;
        }

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($"{{\"mimeType\": \"{contentType}\", \"type\": \"object\", \"schema\": {ProtocolJsonSerializer.ToJson(schema)}}}");
    }
}