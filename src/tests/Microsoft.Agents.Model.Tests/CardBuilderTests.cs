// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Text;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

#pragma warning disable CS0618 // Type or member is obsolete - exercising obsolete BasicCard/MediaCard builders

namespace Microsoft.Agents.Model.Tests
{
    public class CardBuilderTests
    {
        [Fact]
        public void HeroCardFluentBuilders()
        {
            var card = new HeroCard { Title = "t", Subtitle = "s", Text = "txt", Tap = new CardAction(type: ActionTypes.OpenUrl) }
                .AddImage("https://img", "alt")
                .AddImage(new CardImage("https://img2"))
                .AddButton("Yes", value: "yes")
                .AddButton(new CardAction(type: ActionTypes.PostBack, title: "No"))
                .AddButtons(new CardAction(title: "A"), new CardAction(title: "B"));

            Assert.Equal("t", card.Title);
            Assert.Equal("s", card.Subtitle);
            Assert.Equal("txt", card.Text);
            Assert.Equal(ActionTypes.OpenUrl, card.Tap.Type);
            Assert.Equal(2, card.Images.Count);
            Assert.Equal("https://img", card.Images[0].Url);
            Assert.Equal("alt", card.Images[0].Alt);
            Assert.Equal(4, card.Buttons.Count);
            Assert.Equal("Yes", card.Buttons[0].Title);
            Assert.Equal("yes", card.Buttons[0].Value);
        }

        [Fact]
        public void ThumbnailAndBasicCardBuilders()
        {
            var thumb = new ThumbnailCard { Title = "t" }.AddImage("u").AddButton("b");
            Assert.Equal("t", thumb.Title);
            Assert.Single(thumb.Images);
            Assert.Single(thumb.Buttons);

            var basic = new BasicCard { Text = "x" }.AddImage(new CardImage("u")).AddButton(new CardAction(title: "b"));
            Assert.Equal("x", basic.Text);
            Assert.Single(basic.Images);
            Assert.Single(basic.Buttons);
        }

        [Fact]
        public void MediaCardBuildersAddMediaAndButtons()
        {
            var animation = new AnimationCard { Title = "a" }.AddMedia("https://m").AddButton(new CardAction(title: "b"));
            Assert.Equal("a", animation.Title);
            Assert.Single(animation.Media);
            Assert.Equal("https://m", animation.Media[0].Url);
            Assert.Single(animation.Buttons);

            var audio = new AudioCard().AddMedia(new MediaUrl("https://a"));
            Assert.Single(audio.Media);

            var video = new VideoCard().AddMedia("https://v", "profile");
            Assert.Equal("profile", video.Media[0].Profile);

            var media = new MediaCard { Text = "m" }.AddMedia("https://x");
            Assert.Equal("m", media.Text);
            Assert.Single(media.Media);
        }

        [Fact]
        public void ReceiptCardBuilders()
        {
            var receipt = new ReceiptCard { Title = "r", Total = "$10", Tax = "$1", Tap = new CardAction(type: ActionTypes.OpenUrl) }
                .AddFact(new Fact("key", "value"))
                .AddItem(new ReceiptItem(title: "item"))
                .AddButton(new CardAction(title: "b"));

            Assert.Equal("r", receipt.Title);
            Assert.Equal("$10", receipt.Total);
            Assert.Equal("$1", receipt.Tax);
            Assert.Equal(ActionTypes.OpenUrl, receipt.Tap.Type);
            Assert.Single(receipt.Facts);
            Assert.Single(receipt.Items);
            Assert.Single(receipt.Buttons);
        }

        [Fact]
        public void AdaptiveCardCardFromJsonToAttachment()
        {
            var json = "{\"type\":\"AdaptiveCard\",\"version\":\"1.4\"}";
            var card = new AdaptiveCardCard(json);

            Assert.Equal(json, card.Content);

            var attachment = card.ToAttachment();
            Assert.Equal(ContentTypes.AdaptiveCard, attachment.ContentType);
            Assert.Equal(json, attachment.Content);
        }

        [Fact]
        public void AdaptiveCardCardFromStreamToAttachment()
        {
            var json = "{\"type\":\"AdaptiveCard\",\"version\":\"1.4\"}";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var card = new AdaptiveCardCard(stream);

            Assert.Equal(json, card.Content);
            // Stream is left open.
            Assert.True(stream.CanRead);
        }

        [Fact]
        public void AdaptiveCardCardSerializesContentAsNestedJson()
        {
            var json = "{\"type\":\"AdaptiveCard\",\"version\":\"1.4\"}";
            var activity = ((IActivity)Activity.CreateMessageActivity()).AddCard(new AdaptiveCardCard(json));

            var serialized = ProtocolJsonSerializer.ToJson(activity);

            // Content is unpacked into nested JSON (an object), not an escaped string.
            Assert.Contains("\"content\":{\"type\":\"AdaptiveCard\"", serialized);
            Assert.Equal(ContentTypes.AdaptiveCard, activity.Attachments[0].ContentType);
        }
    }
}
