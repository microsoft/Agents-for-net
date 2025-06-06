// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class UnpackObjectStringsTests
    {
        [Fact(Skip = "Needs to run separately")]
        public void ObjectWithEscapedStrings()
        {
            ProtocolJsonSerializer.SerializationOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            ProtocolJsonSerializer.UnpackObjectStrings = false;

            var incoming = "{\"channelData\":{\"name\":\"myName\"},\"value\":\"{\\\"a\\\": \\\"b\\\"}\"}";
            var a = ProtocolJsonSerializer.ToObject<Activity>(incoming);
            Assert.True(a.Value is string);
            Assert.True(a.ChannelData is JsonElement);

            var outgoing = ProtocolJsonSerializer.ToJson(a);

            var parsedA = JsonSerializer.Deserialize<JsonElement>(incoming);
            var parsedB = JsonSerializer.Deserialize<JsonElement>(outgoing);
            var rawA = parsedA.GetRawText();
            var rawB = parsedB.GetRawText();

            var outActivity = new Activity() { ChannelData = new MyClass() { Name = "myName" }, Value = "\"{\\\"a\\\": \\\"b\\\"}\"" };
            var inJson = ProtocolJsonSerializer.ToJson(outActivity);
            var inActivity = ProtocolJsonSerializer.ToObject<IActivity>(inJson);

            Assert.True(rawA == rawB && rawA == inJson && rawA == JsonSerializer.Deserialize<JsonElement>(inJson).GetRawText());
        }
    }

    class MyClass
    {
        public string Name { get; set; }
    }
}
