// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.State.Tests
{
    public class TestUtilities
    {
        public static TurnContext CreateEmptyContext()
        {
            var b = new TestAdapter();
            var a = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "EmptyContext",
                Conversation = new ConversationAccount
                {
                    Id = "test",
                },
                From = new ChannelAccount
                {
                    Id = "empty@empty.context.org",
                },
            };
            var bc = new TurnContext(b, a);

            return bc;
        }
    }
}
