// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
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
            // type
            .Send(new Activity() { Type = ActivityTypes.Event })
            // selector
            .Send(new Activity() { Type = ActivityTypes.Event, Name = "test" })
            // regex
            .Send(new Activity() { Type = "test1" })
            .Send(new Activity() { Type = "test2" })
            .StartTestAsync();

            // assert
            Assert.Contains("ActivityTypeAsync", app.calls);
            Assert.Contains("ActivityTypeSelectorAsync", app.calls);
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
            // text
            .Send("hi")
            // selector
            .Send("testSelector")
            // regex
            .Send("test1")
            .Send("test2")
            .StartTestAsync();

            // assert
            Assert.Contains("MessageAsync", app.calls);
            Assert.Contains("MessageSelectorAsync", app.calls);
            Assert.Equal(2, app.calls.Where(s => s == "MessageRegexAsync").ToList().Count);
        }

        [Fact]
        public void RouteAttributeTest_SelectorNotFound()
        {
            // arrange
            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage);

            try
            {
                new TestSelectorNotFoundApp(options);
            }
            catch (Exception ex)
            {
                Assert.IsAssignableFrom<ArgumentException>(ex);
                Assert.Equal("The RouteAttribute.Selector method 'ActivitySelector' is not found.", ex.Message);
                return;
            }

            Assert.Fail("RouteAttributeTest_SelectorNotFound did not throw");
        }

        [Fact]
        public void RouteAttributeTest_SelectorInvalid()
        {
            // arrange
            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage);

            try
            {
                new TestSelectorInvalidApp(options);
            }
            catch (Exception ex)
            {
                Assert.IsAssignableFrom<ArgumentException>(ex);
                Assert.Equal("The RouteAttribute.Selector method 'BadActivitySelector' does not match the RouteSelector delegate definition.", ex.Message);
                return;
            }

            Assert.Fail("RouteAttributeTest_SelectorInvalid did not throw");
        }

        [Fact]
        public void RouteAttributeTest_MissingArgs()
        {
            // arrange
            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage);

            try
            {
                new TestMissingArgsApp(options);
            }
            catch (Exception ex)
            {
                Assert.IsAssignableFrom<ArgumentException>(ex);
                Assert.Equal("A RouteAttribute is missing required arguments.", ex.Message);
                return;
            }

            Assert.Fail("RouteAttributeTest_MissingArgs did not throw");
        }
    }

    class TestActivityTypeApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Event)]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task ActivityTypeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(ActivityTypeAsync));
            return Task.CompletedTask;
        }

        protected Task<bool> ActivitySelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == "test");
        }

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity, Selector = "ActivitySelector", Rank = RouteRank.First)]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task ActivityTypeSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(ActivityTypeSelectorAsync));
            return Task.CompletedTask;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity, Regex = "test*.")]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task ActivityTypeRegexAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(ActivityTypeRegexAsync));
            return Task.CompletedTask;
        }
    }

    class TestSelectorNotFoundApp(AgentApplicationOptions options) : AgentApplication(options)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity, Selector = "ActivitySelector")]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task ActivityTypeSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    class TestSelectorInvalidApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        // improper signature intentional
        protected Task<bool> BadActivitySelector(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.FromResult(turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == "test");
        }

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity, Selector = "BadActivitySelector")]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task ActivityTypeSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    class TestMissingArgsApp(AgentApplicationOptions options) : AgentApplication(options)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity)]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task ActivityTypeSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    class TestMessageApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Message, Text = "hi")]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task MessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(MessageAsync));
            return Task.CompletedTask;
        }

        protected Task<bool> MessageSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(turnContext.Activity.Text == "testSelector");
        }

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Activity, Selector = "MessageSelector", Rank = RouteRank.First)]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task MessageSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(MessageSelectorAsync));
            return Task.CompletedTask;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        [Route(RouteType = RouteType.Message, Regex = "test*.")]
#pragma warning restore CS0618 // Type or member is obsolete
        protected Task MessageRegexAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add(nameof(MessageRegexAsync));
            return Task.CompletedTask;
        }
    }
}
