// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ObjectSerializationTests
    {
        [Fact]
        public void ActivityValueStringSerialize()
        {
            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Value = "10"
            };

            var inActivity = RoundTrip(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<string>(inActivity.Value);
            Assert.Equal(outActivity.Value, inActivity.Value);
        }

        [Fact]
        public void ActivityValueNumberSerialize()
        {
            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Value = 10
            };

            var inActivity = RoundTrip(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<int>(inActivity.Value);
            Assert.Equal(outActivity.Value, inActivity.Value);
        }

        [Fact]
        public void ActivityValueBooleanSerialize()
        {
            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Value = true
            };

            var inActivity = RoundTrip(outActivity);

            Assert.NotNull(inActivity);
            Assert.IsType<bool>(inActivity.Value);
            Assert.Equal(outActivity.Value, inActivity.Value);
        }

        [Fact]
        public void ActivityValueObjectSerialize()
        {
            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Value = new { key1 = "1", key2 = 1 }
            };

            var inActivity = RoundTrip(outActivity);

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

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                ChannelData = "testData"
            };

            var inActivity = RoundTrip(outActivity);

            Assert.IsType<string>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData);
        }

        [Fact]
        public void ChannelDataSerializationStringJsonTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: String JSON data comes back as a JsonElement

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                ChannelData = "{\"stringProperty\":\"stringValue\",\"numberProperty\":10}"
            };

            var inActivity = RoundTrip(outActivity);

            Assert.IsType<JsonElement>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData.ToString());
        }

        [Fact]
        public void ChannelDataSerializationNumberTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: int data comes back as an int

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                ChannelData = 1
            };

            var inActivity = RoundTrip(outActivity);

            Assert.IsType<int>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData);
        }

        [Fact]
        public void ChannelDataSerializationBooleanTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: Boolean data comes back as a boolean

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                ChannelData = true
            };

            var inActivity = RoundTrip(outActivity);

            Assert.IsType<bool>(inActivity.ChannelData);
            Assert.Equal(outActivity.ChannelData, inActivity.ChannelData);
        }

        [Fact]
        public void ChannelDataSerializationObjectTest()
        {
            // Activity.ChannelData has special semantics.
            //
            // This test: Object data comes back as a JsonElement

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                ChannelData = new { key1 = "1", key2 = 1 }
            };

            var inActivity = RoundTrip(outActivity);

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

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                ChannelData = new[] { "test1", "test2" }
            };

            var inActivity = RoundTrip(outActivity);

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

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip(outActivity);

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

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip(outActivity);

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

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip(outActivity);

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

            var outActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "test",
                SuggestedActions = suggestedActions
            };

            var inActivity = RoundTrip(outActivity);

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

            var activity = ProtocolJsonSerializer.ToObject<Activity>(text);
            AssertPropertyValues(activity);

            var json = ProtocolJsonSerializer.ToJson(activity);
            var activity2 = ProtocolJsonSerializer.ToObject<Activity>(json);

            AssertPropertyValues(activity2);
        }

        [Fact]
        public void SubChannelTests()
        {
            //Channels values
            //public const string Msteams = "msteams";
            //public const string M365CopilotSubChannel = "COPILOT";
            //public const string M365Copilot = $"{Msteams}:{M365CopilotSubChannel}";

            // Is Teams?
            string msTeamsJson = "{\"channelId\":\"msteams\"}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(msTeamsJson);
            Assert.True(Channels.Msteams == activity.ChannelId);

            // Case insensitive
            msTeamsJson = "{\"channelId\":\"mStEaMs\"}";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(msTeamsJson);
            Assert.True(Channels.Msteams == activity.ChannelId);

            // Is M365Copilot?
            string m365CopilotJson = "{\"channelId\":\"msteams\",\"entities\":[{\"channelId\":\"COPILOT\",\"type\":\"subChannelInfo\"}]}";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(m365CopilotJson);
            Assert.True(Channels.M365Copilot == activity.ChannelId);

            // Base channel vs subchannel eval
            Assert.Equal(Channels.Msteams, activity.ChannelId.Channel);
            Assert.Equal(Channels.M365CopilotSubChannel, activity.ChannelId.SubChannel);

            // Serialize back out correctly
            var json = ProtocolJsonSerializer.ToJson(activity);
            Assert.Equal(m365CopilotJson, json);

            // ChannelId construction
            var channelId = new ChannelId(Channels.Msteams);
            Assert.Equal(Channels.Msteams, channelId.ToString());

            // With formatted value
            channelId = new ChannelId(Channels.M365Copilot);
            Assert.Equal(Channels.M365Copilot, channelId.ToString());
            Assert.True(Channels.M365Copilot == activity.ChannelId);
            Assert.Equal(Channels.Msteams, activity.ChannelId.Channel);
            Assert.Equal(Channels.M365CopilotSubChannel, activity.ChannelId.SubChannel);

            // nulls
            activity = new Activity() { ChannelId = null };
            Assert.False(Channels.Msteams == activity.ChannelId);

            activity = new Activity() { ChannelId = Channels.Msteams };
            Assert.False(activity.ChannelId == null);

            // Equality
            var channelId1 = new ChannelId(Channels.Msteams);
            var channelId2 = new ChannelId(Channels.Msteams);
            var channelId3 = new ChannelId(Channels.M365Copilot);
            Assert.Equal(channelId1, channelId2);
            Assert.NotEqual(channelId1, channelId3);
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


        private Activity RoundTrip(Activity outActivity)
        {
            var json = ProtocolJsonSerializer.ToJson(outActivity);
            return ProtocolJsonSerializer.ToObject<Activity>(json);
        }

        private void AssertPropertyValues(Activity activity)
        {
            activity = activity ?? throw new ArgumentNullException(nameof(activity));
            AssertPropertyValue("cci_content_version", "1286428", activity);
            AssertPropertyValue("cci_tenant_id", "9f6be790-4a16-4dd6-9850-44a0d2649aef", activity);
            AssertPropertyValue("cci_bot_id", "215797fa-5550-4f12-a967-c15437884964", activity);
            AssertPropertyValue("cci_user_token", "secret", activity);

            // Validate lists
            Assert.NotEmpty(activity.MembersAdded);
            Assert.NotEmpty(activity.MembersRemoved);    
            Assert.NotEmpty(activity.ReactionsAdded);
            Assert.NotEmpty(activity.ReactionsRemoved);

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

        private void AssertPropertyValue(string propertyName, string expectedValue, Activity activity)
        {
            var actualValue = activity.Properties[propertyName].GetString();
            Assert.Equal(expectedValue, actualValue);
        }

        private class TestObjectClass
        {
            public string ObjectName { get; set; }

            public TestObjectClass TestObject { get; set; }
        }
    }
}
