// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class OnTurnErrorTests
    {
        [Fact]
        public async Task OnTurnError_Test()
        {
            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation("OnTurnError_Test"));
            adapter.OnTurnError = async (context, exception) =>
            {
                if (exception is NotImplementedException)
                {
                    var reply = new MessageActivity
                    {
                        Text = exception.Message,
                        Conversation = context.Activity.Conversation,
                        From = context.Activity.Recipient,
                        Recipient = context.Activity.From,
                        ReplyToId = context.Activity.Id
                    };
                    await context.SendActivityAsync(reply, CancellationToken.None);
                }
                else
                {
                    await context.SendActivityAsync("Unexpected exception");
                }
            };

            await new TestFlow(adapter, (context, cancellationToken) =>
                {
                    if (context.Activity is IMessageActivity msg && msg.Text == "foo")
                    {
                        context.SendActivityAsync(msg.Text);
                    }

                    if (context.Activity is IMessageActivity msgEx && msgEx.Text == "NotImplementedException")
                    {
                        throw new NotImplementedException("Test");
                    }

                    return Task.CompletedTask;
                })
                .Send("foo")
                .AssertReply("foo", "passthrough")
                .Send("NotImplementedException")
                .AssertReply("Test")
                .StartTestAsync();
        }
    }
}
