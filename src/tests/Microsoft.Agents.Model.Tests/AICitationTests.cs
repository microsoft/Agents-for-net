// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AICitationTests
    {
        [Fact]
        public void AIEntity_Roundtrip()
        {
            // Arrange
#pragma warning disable CS0618 // Type or member is obsolete
            var entityOut = new AIEntity()
            {
                Citation = [
                    new ClientCitation()
                    {
                        Position = 1,
                        Appearance = new ClientCitationAppearance()
                        {
                            Name = "name",
                            Text = "text",
                            Url = "url",
                            Image = new AppearanceImage()
                            {
                                Name = ClientCitationsIconNameEnum.Image,// "image",
                            }
                        }
                    }
                ]
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var expected = "{\"@type\":\"Message\",\"@context\":\"https://schema.org\",\"@id\":\"\",\"additionalType\":[],\"citation\":[{\"position\":1,\"appearance\":{\"name\":\"name\",\"text\":\"text\",\"url\":\"url\",\"image\":{\"type\":\"ImageObject\",\"name\":\"Image\"},\"@type\":\"DigitalDocument\"},\"@type\":\"Claim\"}],\"type\":\"https://schema.org/Message\"}";
   
            // Test serialize
            var jsonOut = ProtocolJsonSerializer.ToJson(entityOut);
            Assert.Equal(expected, jsonOut);

            // Test deserialize
            var entityIn = ProtocolJsonSerializer.ToObject<Entity>(jsonOut);
            Assert.IsType<AIEntity>(entityIn);

            var deserializeJson = ProtocolJsonSerializer.ToJson(entityIn);
            Assert.Equal(expected, deserializeJson);
        }

        [Fact]
        public void AIEntity_Properties()
        {
            var jsonIn = "{\"@type\":\"Message\",\"@context\":\"https://schema.org\",\"@id\":\"\",\"additionalType\":[\"AIGeneratedContent\"],\"citation\":[],\"type\":\"https://schema.org/Message\",\"customProperty\":\"customValue\",\"additional\":{\"myprops\":{\"a\":\"b\"}}}";

            var entity = ProtocolJsonSerializer.ToObject<Entity>(jsonIn);
            Assert.IsAssignableFrom<AIEntity>(entity);

            var jsonOut = ProtocolJsonSerializer.ToJson(entity);
            Assert.Equal(jsonIn, jsonOut);
        }

        [Theory]
        [InlineData("msteams")]
        [InlineData("webchat")]
        public void AIEntity_NestedSensitivityIds_RoundTripAcrossChannels(string channelId)
        {
            var activity = ProtocolJsonSerializer.ToObject<Activity>(
                """
                {"type":"message","entities":[{"type":"https://schema.org/Message","@type":"Message","@context":"https://schema.org","@id":"","usageInfo":{"@type":"CreativeWork","@id":"urn:example:message-label","name":"Message label","position":1},"citation":[{"@type":"Claim","position":1,"appearance":{"@type":"DigitalDocument","name":"Source","usageInfo":{"@type":"CreativeWork","@id":"urn:example:citation-label","description":"Citation label","position":2}}}]}]}
                """);
            activity.ChannelId = channelId;

            using var json = JsonDocument.Parse(ProtocolJsonSerializer.ToJson(activity));
            var entity = json.RootElement.GetProperty("entities")[0];
            Assert.Equal("", entity.GetProperty("@id").GetString());
            Assert.Equal("https://schema.org", entity.GetProperty("@context").GetString());
            var messageUsage = entity.GetProperty("usageInfo");
            Assert.True(messageUsage.TryGetProperty("@id", out var messageId));
            Assert.Equal("urn:example:message-label", messageId.GetString());
            Assert.Equal("Message label", messageUsage.GetProperty("name").GetString());
            Assert.Equal(1, messageUsage.GetProperty("position").GetInt32());
            var citationUsage = entity.GetProperty("citation")[0].GetProperty("appearance").GetProperty("usageInfo");
            Assert.True(citationUsage.TryGetProperty("@id", out var citationId));
            Assert.Equal("urn:example:citation-label", citationId.GetString());
            Assert.Equal("Citation label", citationUsage.GetProperty("description").GetString());
            Assert.Equal(2, citationUsage.GetProperty("position").GetInt32());
        }

        [Theory]
        [InlineData("""{"name":"Legacy label","position":2}""")]
        [InlineData("""{"@id":null,"name":"Legacy label","position":2}""")]
        public void SensitivityUsageInfo_MissingOrNullId_RemainsOmitted(string input)
        {
            var usageInfo = ProtocolJsonSerializer.ToObject<SensitivityUsageInfo>(input);

            using var json = JsonDocument.Parse(ProtocolJsonSerializer.ToJson(usageInfo));
            Assert.False(json.RootElement.TryGetProperty("@id", out _));
            Assert.Equal("Legacy label", json.RootElement.GetProperty("name").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("position").GetInt32());
        }
    }
}
