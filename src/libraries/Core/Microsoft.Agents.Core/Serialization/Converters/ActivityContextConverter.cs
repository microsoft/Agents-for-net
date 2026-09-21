// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal sealed class ActivityContextConverter : JsonConverter<ActivityContext>
    {
        public override ActivityContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected a JSON object for {nameof(ActivityContext)}.");
            }

            string traceId = null;
            string spanId = null;
            string traceState = null;
            var traceFlags = ActivityTraceFlags.None;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (traceId == null || spanId == null)
                    {
                        throw new JsonException($"{nameof(ActivityContext)} requires traceId and spanId.");
                    }

                    try
                    {
                        return new ActivityContext(
                            ActivityTraceId.CreateFromString(traceId.AsSpan()),
                            ActivitySpanId.CreateFromString(spanId.AsSpan()),
                            traceFlags,
                            traceState,
                            isRemote: true);
                    }
                    catch (ArgumentOutOfRangeException exception)
                    {
                        throw new JsonException($"Invalid {nameof(ActivityContext)} traceId or spanId.", exception);
                    }
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected a property name while reading {nameof(ActivityContext)}.");
                }

                var propertyName = reader.GetString();
                if (!reader.Read())
                {
                    throw new JsonException($"Unexpected end of JSON while reading {nameof(ActivityContext)}.");
                }

                if (string.Equals(propertyName, "traceId", StringComparison.OrdinalIgnoreCase))
                {
                    traceId = reader.GetString();
                }
                else if (string.Equals(propertyName, "spanId", StringComparison.OrdinalIgnoreCase))
                {
                    spanId = reader.GetString();
                }
                else if (string.Equals(propertyName, "traceFlags", StringComparison.OrdinalIgnoreCase))
                {
                    traceFlags = (ActivityTraceFlags)reader.GetByte();
                }
                else if (string.Equals(propertyName, "traceState", StringComparison.OrdinalIgnoreCase))
                {
                    traceState = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                }
                else if (string.Equals(propertyName, "isRemote", StringComparison.OrdinalIgnoreCase))
                {
                    // Rehydrated contexts are always remote. Just validating the value here, but not using it.
                    _ = reader.GetBoolean();
                }
                else
                {
                    using var _ = JsonDocument.ParseValue(ref reader);
                }
            }

            throw new JsonException($"JSON did not contain the end of {nameof(ActivityContext)}.");
        }

        public override void Write(Utf8JsonWriter writer, ActivityContext value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("traceId", value.TraceId.ToString());
            writer.WriteString("spanId", value.SpanId.ToString());
            writer.WriteNumber("traceFlags", (byte)value.TraceFlags);
            if (value.TraceState != null)
            {
                writer.WriteString("traceState", value.TraceState);
            }
            writer.WriteBoolean("isRemote", value.IsRemote);
            writer.WriteEndObject();
        }
    }
}
