// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Text;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class MentionTests
    {
        [Fact]
        public void MentionInits()
        {
            var mentioned = new ChannelAccount("Id1", "Name1", "Role1", "aadObjectId1");
            var text = "hi @Name1";
            var type = "mention";

            var mention = new Mention(mentioned, text, type);

            Assert.NotNull(mention);
            Assert.IsType<Mention>(mention);
            Assert.Equal(mentioned, mention.Mentioned);
            Assert.Equal(text, mention.Text);
            Assert.Equal(type, mention.Type);
        }

        [Fact]
        public void MentionInitsWithNoArgs()
        {
            var mention = new Mention();

            Assert.NotNull(mention);
            Assert.IsType<Mention>(mention);
        }

        [Fact]
        public void MentionRoundTrip()
        {
            var outMention = new Mention()
            {
                Text = "imamention",
                Mentioned = new ChannelAccount()
                {
                    Id = "id",
                    Name = "name",
                }
            };

            var json = ProtocolJsonSerializer.ToJson(outMention);
            var inEntity = ProtocolJsonSerializer.ToObject<Entity>(json);

            Assert.IsAssignableFrom<Mention>(inEntity);

            var inMention = inEntity as Mention;
            Assert.Equal(outMention.Text, inMention.Text);
            Assert.NotNull(inMention.Mentioned);
            Assert.Equal(outMention.Mentioned.Name, inMention.Mentioned.Name);
            Assert.Equal(outMention.Mentioned.Id, inMention.Mentioned.Id);
        }

        [Fact]
        public void MentionTypedDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"mention\", \"text\": \"hi @Name1\", \"mentioned\": {\"id\": \"Id1\"}}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            Assert.IsType<Mention>(activity.Entities[0]);
            var mention = activity.Entities[0] as Mention;
            Assert.Equal("hi @Name1", mention.Text);
            Assert.NotNull(mention.Mentioned);
            Assert.Equal("Id1", mention.Mentioned.Id);
        }

        [Theory]
        [InlineData("Plain text", "Plain text")]
        [InlineData("<at>Tom</at>", "Tom ")]
        [InlineData("Hi <AT id='123'>Tom</aT>!", "Hi Tom !")]
        [InlineData("<at>Tom</at><at>John</at>", "Tom John ")]
        [InlineData("<at>Tom</at>\t<at>John</at>\n", "Tom\tJohn\n")]
        [InlineData("<at></at>", " ")]
        [InlineData("<at id='x>y'>Tom</at>", "y'>Tom ")]
        public void NormalizeMentions_PreservesFlatTagOutput(string input, string expected)
        {
            var activity = new Activity(ActivityTypes.Message) { Text = input };

            activity.NormalizeMentions(removeMention: false);

            Assert.Equal(expected, activity.Text);
        }

        [Theory]
        [InlineData("<at")]
        [InlineData("<at>Tom")]
        [InlineData("Tom</at>")]
        [InlineData("<at>Tom</at> <at>John")]
        [InlineData("<at>Tom</at> </at>")]
        [InlineData("<at>Outer<at>Inner</at>Tail</at>")]
        [InlineData("<at>Tom</at> </at >")]
        public void NormalizeMentions_PreservesInvalidTagStructure(string input)
        {
            var activity = new Activity(ActivityTypes.Message) { Text = input };

            activity.NormalizeMentions(removeMention: false);

            Assert.Equal(input, activity.Text);
        }

        [Theory]
        [InlineData(" <at>Tom</at> ", "Tom")]
        [InlineData("<at>Outer<at>Inner</at>Tail</at>", "<at>Outer<at>Inner</at>Tail</at>")]
        public void NormalizeMentions_NormalizesAndTrimsMentionEntities(string input, string expected)
        {
            var mention = new Mention(new ChannelAccount("user"), input, "mention");
            var activity = new Activity(ActivityTypes.Message)
            {
                Text = "Plain text",
                Entities = [mention]
            };

            activity.NormalizeMentions(removeMention: false);

            Assert.Equal("Plain text", activity.Text);
            Assert.Equal(expected, mention.Text);
        }

#if NET8_0_OR_GREATER
        [Fact]
        public void NormalizeMentions_RepeatedTags_HasBoundedAllocations()
        {
            const int tagCount = 1024;
            var inputBuilder = new StringBuilder();
            var expectedBuilder = new StringBuilder();
            for (int i = 0; i < tagCount; i++)
            {
                inputBuilder.Append("<at>x</at>");
                expectedBuilder.Append("x ");
            }
            var input = inputBuilder.ToString();
            var expected = expectedBuilder.ToString();
            var activity = new Activity(ActivityTypes.Message) { Text = input };
            new Activity(ActivityTypes.Message) { Text = "<at>x</at>" }.NormalizeMentions(removeMention: false);

            var before = GC.GetAllocatedBytesForCurrentThread();
            activity.NormalizeMentions(removeMention: false);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(expected, activity.Text);
            Assert.InRange(allocated, 0L, (long)input.Length * 16 + 65536);
        }
#endif
    }
}
