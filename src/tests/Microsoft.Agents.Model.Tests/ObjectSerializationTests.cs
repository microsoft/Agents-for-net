// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Models.Activities;
using Microsoft.Agents.Core.Models.Cards;
using Microsoft.Agents.Core.Models.Entities;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ObjectSerializationTests
    {
        [Fact]
        public void ActivityValueStringSerialize()
        {
            var outActivity = new MessageActivity
            {
                Value = "10"
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<string>(inActivity.Value);
            Assert.Equal(outActivity.Value, inActivity.Value);
        }

        [Fact]
        public void ActivityValueNumberSerialize()
        {
            var outActivity = new MessageActivity
            {
                Value = 10
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<int>(inActivity.Value);
            Assert.Equal(outActivity.Value, inActivity.Value);
        }

        [Fact]
        public void ActivityValueBooleanSerialize()
        {
            var outActivity = new MessageActivity
            {
                Value = true
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<bool>(inActivity.Value);
            Assert.Equal(outActivity.Value, inActivity.Value);
        }

        [Fact]
        public void ActivityValueObjectSerialize()
        {
            var outActivity = new MessageActivity
            {
                Value = new { key1 = "1", key2 = 1 }
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<JsonElement>(inActivity.Value);
            var expected = outActivity.Value.ToJsonElements();
            var actual = inActivity.Value.ToJsonElements();
            Assert.Equal(2, actual.Count);
            Assert.Equal(JsonValueKind.String, actual["key1"].ValueKind);
            Assert.Equal("1", actual["key1"].GetString());
            Assert.Equal(JsonValueKind.Number, actual["key2"].ValueKind);
            Assert.Equal(1, actual["key2"].GetInt32());
        }


        [Fact]
        public void ChannelDataSerializationStringTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: String data comes back as a string

            var outActivity = new MessageActivity
            {
                Text = "test",
                ChannelData = "testData"
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<string>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData);
        }

        [Fact]
        public void ChannelDataSerializationStringJsonTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: String JSON data comes back as a JsonElement

            var outActivity = new MessageActivity
            {
                Text = "test",
                ChannelData = "{\"stringProperty\":\"stringValue\",\"numberProperty\":10}"
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<JsonElement>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData.ToString());
        }

        [Fact]
        public void ChannelDataSerializationNumberTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: int data comes back as an int

            var outActivity = new MessageActivity
            {
                Text = "test",
                ChannelData = 1
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<int>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData);
        }

        [Fact]
        public void ChannelDataSerializationBooleanTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: Boolean data comes back as a boolean

            var outActivity = new MessageActivity
            {
                Text = "test",
                ChannelData = true
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<bool>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData);
        }

        [Fact]
        public void ChannelDataSerializationObjectTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: Object data comes back as a JsonElement

            var outActivity = new MessageActivity
            {
                Text = "test",
                ChannelData = new { key1 = "1", key2 = 1 }
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<JsonElement>(inActivity.ChannelData);
            var expected = outActivity.ChannelData.ToJsonElements();
            var actual = inActivity.ChannelData.ToJsonElements();
            Assert.Equal(2, actual.Count);
            Assert.Equal(JsonValueKind.String, actual["key1"].ValueKind);
            Assert.Equal("1", actual["key1"].GetString());
            Assert.Equal(JsonValueKind.Number, actual["key2"].ValueKind);
            Assert.Equal(1, actual["key2"].GetInt32());
        }

        [Fact]
        public void ChannelDataSerializationArrayTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: Array data comes back as a JsonElement

            var outActivity = new MessageActivity
            {
                Text = "test",
                ChannelData = new[] { "test1", "test2" }
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<JsonElement>(inActivity.ChannelData);
            var actualArray = (JsonElement)inActivity.ChannelData;
            Assert.Equal(JsonValueKind.Array, actualArray.ValueKind);
            int count = 0;
            var elements = new List<string>();
            foreach (var element in actualArray.EnumerateArray())
            {
                elements.Add(element.GetString());
                count++;
            }

            Assert.Equal(2, count);
            Assert.Equal("test1", elements[0]);
            Assert.Equal("test2", elements[1]);
        }

        [Fact]
        public void CardActionValueStringSerialize()
        {
            var suggestedActions = new SuggestedActions();
            suggestedActions.Actions.Add(new CardAction()
            {
                Type = ActionTypes.ImBack,
                Title = "title",
                Value = "10"
            });

            var outActivity = new MessageActivity
            {
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<string>(inActivity.SuggestedActions.Actions[0].Value);
            Assert.Equal(outActivity.SuggestedActions.Actions[0].Value, inActivity.SuggestedActions.Actions[0].Value);
        }

        [Fact]
        public void CardActionValueNumberSerialize()
        {
            var suggestedActions = new SuggestedActions();
            suggestedActions.Actions.Add(new CardAction()
            {
                Type = ActionTypes.ImBack,
                Title = "title",
                Value = 10
            });

            var outActivity = new MessageActivity
            {
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<int>(inActivity.SuggestedActions.Actions[0].Value);
            Assert.Equal(outActivity.SuggestedActions.Actions[0].Value, inActivity.SuggestedActions.Actions[0].Value);
        }

        [Fact]
        public void CardActionValueBooleanSerialize()
        {
            var suggestedActions = new SuggestedActions();
            suggestedActions.Actions.Add(new CardAction()
            {
                Type = ActionTypes.ImBack,
                Title = "title",
                Value = true
            });

            var outActivity = new MessageActivity
            {
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<bool>(inActivity.SuggestedActions.Actions[0].Value);
            Assert.Equal(outActivity.SuggestedActions.Actions[0].Value, inActivity.SuggestedActions.Actions[0].Value);
        }

        [Fact]
        public void CardActionValueObjectSerialize()
        {
            var suggestedActions = new SuggestedActions();
            suggestedActions.Actions.Add(new CardAction()
            {
                Type = ActionTypes.ImBack,
                Title = "title",
                Value = new { key1 = "1", key2 = 1 }
            });

            var outActivity = new MessageActivity
            {
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip<IMessageActivity>(outActivity);

            Assert.IsType<JsonElement>(inActivity.SuggestedActions.Actions[0].Value);
            var expected = outActivity.SuggestedActions.Actions[0].Value.ToJsonElements();
            var actual = inActivity.SuggestedActions.Actions[0].Value.ToJsonElements();
            Assert.Equal(2, actual.Count);
            Assert.Equal(JsonValueKind.String, actual["key1"].ValueKind);
            Assert.Equal("1", actual["key1"].GetString());
            Assert.Equal(JsonValueKind.Number, actual["key2"].ValueKind);
            Assert.Equal(1, actual["key2"].GetInt32());
        }

        [Fact]
        public void ComplexActivitySerializationTest()
        {
            var text = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ComplexActivityPayload.json"));

            var activity = ProtocolJsonSerializer.ToObject<IMessageActivity>(text);
            AssertPropertyValues(activity);

            var json = ProtocolJsonSerializer.ToJson(activity);
            var activity2 = ProtocolJsonSerializer.ToObject<IMessageActivity>(json);

            AssertPropertyValues(activity2);
        }

        [Theory]
        [InlineData("cps_event")]
        [InlineData("cps_greeting")]
        [InlineData("cps_suggestedactions")]
        [InlineData("cps_typing")]
        public void ValidateActivitySerializer(string baseFileName)
        {
            var sourceActivity = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Resources", $"{baseFileName}_in.json"));
            var targetActivity = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Resources", $"{baseFileName}_out.json"));
            var outData = JsonSerializer.Deserialize<object>(targetActivity);
            var resultingText = JsonSerializer.Serialize(outData);

            var activity = ProtocolJsonSerializer.ToObject<Activity>(sourceActivity); // Read in the activity from the wire example.

            // convert to Json for Outbound leg
            var outboundJson = activity.ToJson();

            // Compare the outbound JSON to the expected JSON
            Assert.Equal(resultingText, outboundJson);
        }

        [Fact]
        public void ActivityWithDerivedEntitySerializationTest()
        {
            var jsonIn = "{\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[{\"@type\":\"Message\",\"@context\":\"https://schema.org\",\"@id\":\"\",\"additionalType\":[\"AIGeneratedContent\"],\"citation\":[],\"type\":\"https://schema.org/Message\"}],\"listenFor\":[],\"textHighlights\":[]}";

            var activity = ProtocolJsonSerializer.ToObject<Activity>(jsonIn);
            var jsonOut = ProtocolJsonSerializer.ToJson(activity);

            Assert.Equal(jsonIn, jsonOut);
        }



        [Fact]
        public void WithDerivedActivitySerializationTest()
        {
            List<Activity> activities = [new DerivedActivity
            {
                Secret = "secret",
                Public = "public"
            }];
            var jsonOut = ProtocolJsonSerializer.ToJson(activities);
            var expected = "[{\"@public\":\"public\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[],\"listenFor\":[],\"textHighlights\":[]}]";
            
            Assert.Equal(expected, jsonOut);
        }

        [Fact]
        public void SerializeDeserializeIsThreadSafeUnderConcurrency()
        {
            var text = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ComplexActivityPayload.json"));

            var activity = ProtocolJsonSerializer.ToObject<IMessageActivity>(text);

            var json = ProtocolJsonSerializer.ToJson(activity);

            const int threadCount = 50;
            var results = new IMessageActivity[threadCount];

            Parallel.For(0, threadCount, i =>
            {
                results[i] = RoundTrip<IMessageActivity>(activity);
            });

            foreach (var result in results)
            {
                Assert.NotNull(result);
                AssertPropertyValues(result);
            }
        }

        [Fact]
        public void DuplicateNameActivitySerializationShouldThrow()
        {
            // This should throw an exception because the property name 'id' is duplicated.
            var activityJson = new DuplicateNameActivity
            {
                Id = "12345",
                MyId = "67890"
            };
            // Expect an InvalidOperationException from System.Text.Json (Note: The ConnectorConverter is not used here because the compile-time type is 'object').
            Assert.Throws<InvalidOperationException>(() => ProtocolJsonSerializer.ToJson(activityJson));
            // Expect an InvalidOperationException from ConnectorConverter.
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize<Activity>(activityJson, ProtocolJsonSerializer.SerializationOptions));
        }


#if SKIP_EMPTY_LISTS
        [Fact]
        public void EmptyListDoesntSerialzie()
        {
            var activity = new Activity()
            {
                MembersAdded = [],
                MembersRemoved = [],
                ReactionsAdded = [],
                ReactionsRemoved = [],
                Attachments = [],
                Entities = [],
                ListenFor = [],
                TextHighlights = [],
                SuggestedActions = new SuggestedActions()
            };

            // We'll add a single item to SuggestedActions.To.  It should serialize but
            // not the other Actions List.
            activity.SuggestedActions.To.Add("test");

            var json = ProtocolJsonSerializer.ToJson(activity);
            var expected = "{\"suggestedActions\":{\"to\":[\"test\"]}}";
            Assert.Equal(expected, json);
        }
#endif

        private T RoundTrip<T>(IActivity outActivity)
        {
            var json = ProtocolJsonSerializer.ToJson(outActivity);
            return ProtocolJsonSerializer.ToObject<T>(json);
        }

        private void AssertPropertyValues(IMessageActivity activity)
        {
            activity = activity ?? throw new ArgumentNullException(nameof(activity));
            AssertPropertyValue("cci_content_version", "1286428", activity);
            AssertPropertyValue("cci_tenant_id", "9f6be790-4a16-4dd6-9850-44a0d2649aef", activity);
            AssertPropertyValue("cci_bot_id", "215797fa-5550-4f12-a967-c15437884964", activity);
            AssertPropertyValue("cci_user_token", "secret", activity);

            // Validate non-Message fields in IActivity.Properties
            AssertPropertyExists("membersAdded", activity);
            AssertPropertyExists("membersRemoved", activity);
            AssertPropertyExists("reactionsAdded", activity);
            AssertPropertyExists("reactionsRemoved", activity);
            AssertPropertyExists("textHighlights", activity);

            Assert.NotNull(activity.Attachments);
            Assert.NotNull(activity.ListenFor);

            Assert.NotEmpty(activity.Entities);
            Assert.NotNull(activity.GetProductInfoEntity());
            Assert.Equal("directline:subchannel", activity.ChannelId);

            // validate .value, .channeldata and the activity additional properties are present
            Assert.NotNull(activity.Value);
            var valueTestObject = ProtocolJsonSerializer.ToObject<TestObjectClass>(activity.Value);
            valueTestObject = valueTestObject ?? throw new Exception(nameof(valueTestObject));
            AssertTestObjectValues(valueTestObject);

            var channelData = activity.ChannelData;
            Assert.NotNull(channelData);
            var channelDataTestObject = ProtocolJsonSerializer.ToObject<TestObjectClass>(channelData.ToJsonElements()["testChannelDataObject"]);
            channelDataTestObject = channelDataTestObject ?? throw new Exception(nameof(channelDataTestObject));
            AssertTestObjectValues(channelDataTestObject);

            var property = activity.Properties["testActivityObject"];
            var activityTestObject = ProtocolJsonSerializer.ToObject<TestObjectClass>(property);
            activityTestObject = activityTestObject ?? throw new Exception(nameof(activityTestObject));
            AssertTestObjectValues(activityTestObject);
        }

        private void AssertTestObjectValues(TestObjectClass testObject)
        {
            Assert.Equal("level one", testObject.ObjectName);
            Assert.NotNull(testObject.TestObject);
            Assert.Equal("level two", testObject.TestObject?.ObjectName);
        }

        private void AssertPropertyValue(string propertyName, string expectedValue, IActivity activity)
        {
            var actualValue = activity.Properties[propertyName].GetString();
            Assert.Equal(expectedValue, actualValue);
        }

        private void AssertPropertyExists(string propertyName, IActivity activity)
        {
            Assert.True(activity.Properties.ContainsKey(propertyName), $"Property '{propertyName}' does not exist on activity.");
        }

        private class TestObjectClass
        {
            public string ObjectName { get; set; }

            public TestObjectClass TestObject { get; set; }
        }

        private class DerivedActivity : Activity
        {
            [JsonIgnore]
            public string Secret { get; set; }

            [JsonPropertyName("@public")]
            public string Public { get; set; }
        }

        private class DuplicateNameActivity : Activity
        {

            [JsonPropertyName("id")]
            public string MyId { get; set; }
        }
    }
}   