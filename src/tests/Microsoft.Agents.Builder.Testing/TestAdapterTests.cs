// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Models.Activities;
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Testing
{
    public class TestAdapterTests
    {
        [Fact]
        public async Task TestAdapter_ExceptionTypesOnTest()
        {
            string uniqueExceptionId = Guid.NewGuid().ToString();
            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_ExceptionTypesOnTest"));
            await Assert.ThrowsAsync<Exception>(() =>
                  new TestFlow(adapter, async (context, cancellationToken) =>
                  {
                      await context.SendActivityAsync(Activity.CreateReply(context.Activity, () => new MessageActivity("one")), cancellationToken);
                  })
                          .Test("foo", (activity) => throw new Exception(uniqueExceptionId))
                          .StartTestAsync());  
        }

        [Fact]
        public async Task TestAdapter_ExceptionInBotOnReceive()
        {
            string uniqueExceptionId = Guid.NewGuid().ToString();
            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_ExceptionInBotOnReceive"));
            await Assert.ThrowsAsync<Exception>(() => 
                new TestFlow(adapter, (context, cancellationToken) => 
                { 
                    throw new Exception(uniqueExceptionId); 
                })
                    .Test("test", activity => Assert.Null(null), "uh oh!")
                    .StartTestAsync());
        }

        [Fact]
        public async Task TestAdapter_ExceptionTypesOnAssertReply()
        {
            string uniqueExceptionId = Guid.NewGuid().ToString();
            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_ExceptionTypesOnAssertReply"));
            await Assert.ThrowsAsync<Exception>(() =>
                new TestFlow(adapter, async (context, cancellationToken) =>
                {
                    await context.SendActivityAsync(Activity.CreateReply(context.Activity, () => new MessageActivity("one")), cancellationToken);
                })
                    .Send("foo")
                    .AssertReply(
                        (activity) => throw new Exception(uniqueExceptionId), "should throw")

                    .StartTestAsync());
        }

        [Fact]
        public async Task TestAdapter_SaySimple()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_SaySimple"));
            await new TestFlow(adapter, MyBotLogic)
                .Test("foo", "echo:foo", "say with string works")
                .StartTestAsync();
        }

        [Fact]
        public async Task TestAdapter_Say()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_Say"));
            await new TestFlow(adapter, MyBotLogic)
                .Test("foo", "echo:foo", "say with string works")
                .Test("foo", new MessageActivity("echo:foo"), "say with activity works")
                .Test("foo", (activity) => Assert.Equal("echo:foo", (activity as IMessageActivity).Text), "say with validator works")
                .StartTestAsync();
        }

        [Fact]
        public async Task TestAdapter_SendReply()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_SendReply"));
            await new TestFlow(adapter, MyBotLogic)
                .Send("foo").AssertReply("echo:foo", "send/reply with string works")
                .Send("foo").AssertReply(new MessageActivity(text: "echo:foo"), "send/reply with activity works")
                .Send("foo").AssertReply((activity) => Assert.Equal("echo:foo", (activity as IMessageActivity).Text), "send/reply with validator works")
                .StartTestAsync();
        }

        [Fact]
        public async Task TestAdapter_ReplyOneOf()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_ReplyOneOf"));
            await new TestFlow(adapter, MyBotLogic)
                .Send("foo").AssertReplyOneOf(new string[] { "echo:bar", "echo:foo", "echo:blat" }, "say with string works")
                .StartTestAsync();
        }

        [Fact]
        public async Task TestAdapter_MultipleReplies()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_MultipleReplies"));
            await new TestFlow(adapter, MyBotLogic)
                .Send("foo").AssertReply("echo:foo")
                .Send("bar").AssertReply("echo:bar")
                .Send("ignore")
                .Send("count")
                    .AssertReply("one")
                    .AssertReply("two")
                    .AssertReply("three")
                .StartTestAsync();
        }

        [Fact]
        public async Task TestAdapter_TestFlow_SecurityException()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_TestFlow_SecurityException"));

            TestFlow testFlow = new TestFlow(adapter, (ctx, cancellationToken) =>
                {
                    Exception innerException = (Exception)Activator.CreateInstance(typeof(SecurityException));
                    var taskSource = new TaskCompletionSource<bool>();
                    taskSource.SetException(innerException);
                    return taskSource.Task;
                })
                .Send(new Activity());
            await testFlow.StartTestAsync()
                .ContinueWith(action =>
                {
                    Assert.IsType<SecurityException>(action.Exception.InnerException);
                });
        }

        [Fact]
        public async Task TestAdapter_TestFlow_ArgumentException()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_TestFlow_ArgumentException"));

            TestFlow testFlow = new TestFlow(adapter, (ctx, cancellationToken) =>
            {
                Exception innerException = (Exception)Activator.CreateInstance(typeof(ArgumentException));
                var taskSource = new TaskCompletionSource<bool>();
                taskSource.SetException(innerException);
                return taskSource.Task;
            })
                .Send(new Activity());
            await testFlow.StartTestAsync()
                .ContinueWith(action =>
                {
                    Assert.IsType<ArgumentException>(action.Exception.InnerException);
                });
        }

        [Fact]
        public async Task TestAdapter_TestFlow_ArgumentNullException()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("TestAdapter_TestFlow_ArgumentNullException"));

            TestFlow testFlow = new TestFlow(adapter, (ctx, cancellationToken) =>
            {
                Exception innerException = (Exception)Activator.CreateInstance(typeof(ArgumentNullException));
                var taskSource = new TaskCompletionSource<bool>();
                taskSource.SetException(innerException);
                return taskSource.Task;
            })
                .Send(new Activity());
            await testFlow.StartTestAsync()
                .ContinueWith(action =>
                {
                    Assert.IsType<ArgumentNullException>(action.Exception.InnerException);
                });
        }

        [Theory]
        [InlineData(Channels.Test)]
        [InlineData(Channels.Emulator)]
        [InlineData(Channels.Msteams)]
        [InlineData(Channels.Webchat)]
        [InlineData(Channels.Cortana)]
        [InlineData(Channels.Directline)]
        [InlineData(Channels.Facebook)]
        [InlineData(Channels.Slack)]
        [InlineData(Channels.Telegram)]
        public async Task ShouldUseCustomChannelId(string targetChannel)
        {
            var sut = new TestAdapter(targetChannel);

            var receivedChannelId = string.Empty;
            async Task TestCallback(ITurnContext context, CancellationToken token)
            {
                receivedChannelId = context.Activity.ChannelId;
                await context.SendActivityAsync("test reply from the bot", cancellationToken: token);
            }

            await sut.SendTextToBotAsync("test message", TestCallback, CancellationToken.None);
            var reply = sut.GetNextReply();
            Assert.Equal(targetChannel, receivedChannelId);
            Assert.Equal(targetChannel, reply.ChannelId);
        }

        private async Task MyBotLogic(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity is IMessageActivity message)
            {
                switch (message.Text)
                {
                    case "count":
                        await turnContext.SendActivityAsync(Activity.CreateReply(message, () => new MessageActivity("one")), cancellationToken);
                        await turnContext.SendActivityAsync(Activity.CreateReply(message, () => new MessageActivity("two")), cancellationToken);
                        await turnContext.SendActivityAsync(Activity.CreateReply(message, () => new MessageActivity("three")), cancellationToken);
                        break;
                    case "ignore":
                        break;
                    default:
                        await turnContext.SendActivityAsync(
                            Activity.CreateReply(message, () => new MessageActivity($"echo:{message.Text}")), cancellationToken);
                        break;
                }
            }
        }
    }
}
