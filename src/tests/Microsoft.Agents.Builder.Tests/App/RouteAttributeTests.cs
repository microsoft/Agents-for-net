// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class RouteAttributeTests
    {
        [Fact]
        public async Task RouteAttributeTest_ActivityType()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            var options = new TestApplicationOptions(storage);
            var app = new TestActivityTypeApp(options);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            // type match
            .Send(new EventActivity("something"))
            // regex match
            .Send(new Activity("test1"))
            .Send(new Activity("test2"))
            .StartTestAsync();

            // assert
            Assert.Contains("ActivityTypeAsync", app.calls);
            Assert.Equal(2, app.calls.Where(s => s == "ActivityTypeRegexAsync").ToList().Count);
        }

        [Fact]
        public async Task RouteAttributeTest_Message()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            var options = new TestApplicationOptions(storage);
            var app = new TestMessageApp(options);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            // text match
            .Send("hi")
            // regex match
            .Send("test1")
            .Send("test2")
            .StartTestAsync();

            // assert
            Assert.Contains("MessageAsync", app.calls);
            Assert.Equal(2, app.calls.Where(s => s == "MessageRegexAsync").ToList().Count);
        }
    }

    class TestActivityTypeApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [ActivityRoute(ActivityTypes.Event)]
        protected Task ActivityTypeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(ActivityTypeAsync));
            return Task.CompletedTask;
        }

        [ActivityRoute(typeRegex: "test.+")]
        protected Task ActivityTypeRegexAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(ActivityTypeRegexAsync));
            return Task.CompletedTask;
        }
    }

    class TestMessageApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MessageRoute("hi")]
        protected Task MessageAsync(ITurnContext<IMessageActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(MessageAsync));
            return Task.CompletedTask;
        }

        [MessageRoute(textRegex: "test.+")]
        protected Task MessageRegexAsync(ITurnContext<IMessageActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(MessageRegexAsync));
            return Task.CompletedTask;
        }
    }
}
