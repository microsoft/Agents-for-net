// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

internal static class A2AExtensions
{
    private static readonly ConcurrentDictionary<string, JsonNode> _schemas = new();
    private static readonly JsonSchemaExporterOptions _exporterOptions = new() { TreatNullObliviousAsNonNullable = true };
    private static readonly JsonSerializerOptions _reflectionOptions = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(AIJsonUtilities.DefaultOptions.TypeInfoResolver, new DefaultJsonTypeInfoResolver())
    };

    public static Dictionary<string, JsonElement> ToA2AMetadata(this object data, string contentType)
    {
        if (!_schemas.TryGetValue(data.GetType().FullName, out JsonNode schema))
        {
            schema = _reflectionOptions.GetJsonSchemaAsNode(data.GetType(), _exporterOptions);
            _schemas[data.GetType().FullName] = schema;
        }

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($"{{\"mimeType\": \"{contentType}\", \"type\": \"object\", \"schema\": {ProtocolJsonSerializer.ToJson(schema)}}}");
    }
}