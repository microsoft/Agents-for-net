// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ActivityContextSerializationTests
    {
        [Fact]
        public void ActivityContextDeserializesAsRemote()
        {
            var expected = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded,
                "vendor=value",
                isRemote: false);

            var json = JsonSerializer.Serialize(expected, ProtocolJsonSerializer.SerializationOptions);
            var actual = JsonSerializer.Deserialize<ActivityContext>(
                json,
                ProtocolJsonSerializer.SerializationOptions);

            Assert.Equal(expected.TraceId, actual.TraceId);
            Assert.Equal(expected.SpanId, actual.SpanId);
            Assert.Equal(expected.TraceFlags, actual.TraceFlags);
            Assert.Equal(expected.TraceState, actual.TraceState);
            Assert.False(expected.IsRemote);
            Assert.True(actual.IsRemote);
        }

        [Fact]
        public void ActivityContextUsesStringIdentifiers()
        {
            var context = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var json = JsonSerializer.Serialize(context, ProtocolJsonSerializer.SerializationOptions);
            using var document = JsonDocument.Parse(json);

            Assert.Equal(context.TraceId.ToString(), document.RootElement.GetProperty("traceId").GetString());
            Assert.Equal(context.SpanId.ToString(), document.RootElement.GetProperty("spanId").GetString());
        }
    }
}
