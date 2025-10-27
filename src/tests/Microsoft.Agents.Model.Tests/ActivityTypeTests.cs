using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ActivityTypeTests
    {
        [Fact]
        public void ActivityType_CompatValues()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.True(ActivityType.ContactRelationUpdate == ActivityTypes.ContactRelationUpdate);
            Assert.True(ActivityType.ConversationUpdate == ActivityTypes.ConversationUpdate);
            Assert.True(ActivityType.EndOfConversation == ActivityTypes.EndOfConversation);
            Assert.True(ActivityType.Event == ActivityTypes.Event);
            Assert.True(ActivityType.DeleteUserData == ActivityTypes.DeleteUserData);
            Assert.True(ActivityType.Handoff == ActivityTypes.Handoff);
            Assert.True(ActivityType.InstallationUpdate == ActivityTypes.InstallationUpdate);
            Assert.True(ActivityType.Invoke == ActivityTypes.Invoke);
            Assert.True(ActivityType.Message == ActivityTypes.Message);
            Assert.True(ActivityType.MessageDelete == ActivityTypes.MessageDelete);
            Assert.True(ActivityType.MessageReaction == ActivityTypes.MessageReaction);
            Assert.True(ActivityType.MessageUpdate == ActivityTypes.MessageUpdate);
            Assert.True(ActivityType.Suggestion == ActivityTypes.Suggestion);
            Assert.True(ActivityType.Trace == ActivityTypes.Trace);
            Assert.True(ActivityType.Typing == ActivityTypes.Typing);
            Assert.True(ActivityType.Command == ActivityTypes.Command);
            Assert.True(ActivityType.CommandResult == ActivityTypes.CommandResult);
            Assert.True(ActivityType.InvokeResponse == ActivityTypes.InvokeResponse);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void ActivityType_Comparisons()
        {
            Assert.Equal(ActivityType.Message, new Activity("Message").Type);    // uses string.Equals
            Assert.Equal(ActivityType.Message, new Activity("mEsSaGe").Type);    // uses string.Equals
            Assert.True((new Activity("mEsSaGe")).Type == ActivityType.Message); // uses ActivityType.Equals
            Assert.True((new Activity("MyActivityType")).Type == "myactivitytype"); // uses ActivityType.Equals
            Assert.Equal("custom", new Activity("custom").Type);
            Assert.False((new Activity()).Type == ActivityType.Message);
            Assert.False(ActivityType.Message == (new Activity()).Type);
        }

        [Fact]
        public void ActivityType_RoundTrip()
        {
            // odd casing
            var json = "{\"type\": \"mEsSaGe\"}";
            var inActivity = ProtocolJsonSerializer.ToObject<IActivity>(json);
            Assert.Equal(ActivityType.Message, inActivity.Type);

            // serialized per ActivityType string value
            var expected = $"{{\"type\":\"{ActivityType.Message}\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[],\"listenFor\":[],\"textHighlights\":[]}}";
            Assert.Equal(expected, ProtocolJsonSerializer.ToJson(inActivity));

            json = "{\"type\": \"message\"}";
            inActivity = ProtocolJsonSerializer.ToObject<IActivity>(json);
            Assert.Equal(ActivityType.Message, inActivity.Type);

            // serialize
            Assert.Equal(expected, ProtocolJsonSerializer.ToJson(inActivity));
        }
    }
}
