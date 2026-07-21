// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class BuilderHelpersTests
    {
        [Fact]
        public void SuggestedActionsFluentAdders()
        {
            var suggested = new SuggestedActions()
                .AddRecipients("r1", "r2")
                .AddAction(new CardAction(title: "a"))
                .AddActions(new CardAction(title: "b"), new CardAction(title: "c"));

            Assert.Equal(new[] { "r1", "r2" }, suggested.To);
            Assert.Equal(3, suggested.Actions.Count);
            Assert.Equal("a", suggested.Actions[0].Title);
            Assert.Equal("c", suggested.Actions[2].Title);
        }

        [Fact]
        public void GetAccountMentionReturnsMatch()
        {
            var account = new ChannelAccount(id: "u1", name: "User One");
            var activity = Activity.CreateMessageActivity().AddMention(account);

            var mention = activity.GetAccountMention("u1");
            Assert.NotNull(mention);
            Assert.Equal("u1", mention.Mentioned.Id);

            Assert.Null(activity.GetAccountMention("other"));
            Assert.Null(activity.GetAccountMention(null));
        }

        [Fact]
        public void IsRecipientMentioned()
        {
            var recipient = new ChannelAccount(id: "bot", name: "Bot");
            var activity = Activity.CreateMessageActivity();
            activity.Recipient = recipient;

            Assert.False(activity.IsRecipientMentioned());

            activity.AddMention(recipient);
            Assert.True(activity.IsRecipientMentioned());
        }
    }
}
