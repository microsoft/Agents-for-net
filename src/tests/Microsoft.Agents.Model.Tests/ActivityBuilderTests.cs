// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ActivityBuilderTests
    {
        [Fact]
        public void FluentSettersChainAndSetProperties()
        {
            var activity = Activity.CreateMessageActivity()
                .WithText("hello")
                .WithSpeak("hello there")
                .WithInputHint(InputHints.ExpectingInput)
                .WithSummary("a summary")
                .WithLocale("en-US")
                .WithTextFormat(TextFormatTypes.Markdown)
                .WithAttachmentLayout(AttachmentLayoutTypes.Carousel)
                .WithDeliveryMode(DeliveryModes.Normal)
                .WithName("theName")
                .WithValue("theValue", "theValueType");

            Assert.Equal("hello", activity.Text);
            Assert.Equal("hello there", activity.Speak);
            Assert.Equal(InputHints.ExpectingInput, activity.InputHint);
            Assert.Equal("a summary", activity.Summary);
            Assert.Equal("en-US", activity.Locale);
            Assert.Equal(TextFormatTypes.Markdown, activity.TextFormat);
            Assert.Equal(AttachmentLayoutTypes.Carousel, activity.AttachmentLayout);
            Assert.Equal(DeliveryModes.Normal, activity.DeliveryMode);
            Assert.Equal("theName", activity.Name);
            Assert.Equal("theValue", activity.Value);
            Assert.Equal("theValueType", activity.ValueType);
        }

        [Fact]
        public void AddTextAppends()
        {
            var activity = Activity.CreateMessageActivity().WithText("foo").AddText("bar");
            Assert.Equal("foobar", activity.Text);
        }

        [Fact]
        public void AddAttachmentAddsAttachments()
        {
            var activity = Activity.CreateMessageActivity()
                .AddAttachment(new Attachment(contentType: "a"), new Attachment(contentType: "b"));

            Assert.Equal(2, activity.Attachments.Count);
            Assert.Equal("a", activity.Attachments[0].ContentType);
            Assert.Equal("b", activity.Attachments[1].ContentType);
        }

        [Fact]
        public void AddAttachmentWrapsCards()
        {
            var activity = Activity.CreateMessageActivity()
                .AddCard(new HeroCard(title: "hero"))
                .AddCard(new ThumbnailCard(title: "thumb"))
                .AddCard(new OAuthCard())
                .AddCard(new SigninCard());

            Assert.Equal(4, activity.Attachments.Count);
            Assert.Equal(HeroCard.ContentType, activity.Attachments[0].ContentType);
            Assert.IsType<HeroCard>(activity.Attachments[0].Content);
            Assert.Equal(ThumbnailCard.ContentType, activity.Attachments[1].ContentType);
            Assert.Equal(OAuthCard.ContentType, activity.Attachments[2].ContentType);
            Assert.Equal(SigninCard.ContentType, activity.Attachments[3].ContentType);
        }

        [Fact]
        public void AddEntityAddsEntities()
        {
            var activity = Activity.CreateMessageActivity().AddEntity(new Entity("myType"));
            Assert.Single(activity.Entities);
            Assert.Equal("myType", activity.Entities[0].Type);
        }

        [Fact]
        public void AddMentionAddsEntityAndText()
        {
            var account = new ChannelAccount(id: "u1", name: "User One");
            var activity = Activity.CreateMessageActivity().WithText("hi").AddMention(account);

            Assert.Equal("<at>User One</at> hi", activity.Text);
            var mention = Assert.IsType<Mention>(activity.Entities[0]);
            Assert.Equal("u1", mention.Mentioned.Id);
            Assert.Equal("<at>User One</at>", mention.Text);
        }

        [Fact]
        public void AddMentionCanSkipText()
        {
            var account = new ChannelAccount(id: "u1", name: "User One");
            var activity = Activity.CreateMessageActivity().WithText("hi").AddMention(account, text: "Custom", addText: false);

            Assert.Equal("hi", activity.Text);
            var mention = Assert.IsType<Mention>(activity.Entities[0]);
            Assert.Equal("<at>Custom</at>", mention.Text);
        }

        [Fact]
        public void IsTypeHelpers()
        {
            Assert.True(Activity.CreateMessageActivity().IsMessage());
            Assert.True(Activity.CreateTypingActivity().IsTyping());
            Assert.True(Activity.CreateEventActivity().IsEvent());
            Assert.True(Activity.CreateInvokeActivity().IsInvoke());
            Assert.True(Activity.CreateConversationUpdateActivity().IsConversationUpdate());
            Assert.True(Activity.CreateEndOfConversationActivity().IsEndOfConversation());
            Assert.True(Activity.CreateHandoffActivity().IsHandoff());
            Assert.False(Activity.CreateMessageActivity().IsInvoke());
        }
    }
}
