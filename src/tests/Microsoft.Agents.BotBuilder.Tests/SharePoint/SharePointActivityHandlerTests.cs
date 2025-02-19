// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;
using Microsoft.Agents.BotBuilder.SharePoint;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.SharePoint.Models;
using System;

namespace Microsoft.Agents.BotBuilder.Tests.SharePoint
{
    public class SharePointActivityHandlerTests
    {
        private IActivity[] _activitiesToSend = null;

        public SharePointActivityHandlerTests()
        {
            _activitiesToSend = null;
        }

        void CaptureSend(IActivity[] arg)
        {
            _activitiesToSend = arg;
        }

        [Fact]
        public async Task OnInvokeActivityAsync_ShouldThrowOnNullActivityName()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();

            // Assert
            await Assert.ThrowsAsync<NotSupportedException>(() => ((IBot)bot).OnTurnAsync(turnContext));
        }

        [Fact]
        public async Task OnInvokeActivityAsync_ShouldCatchInvokeResponseException()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getCardView",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new ErrorActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(501, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        [Fact]
        public async Task OnInvokeActivityAsync_ShouldCatchBadRequestError()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getCardView",
                Value = 1
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(400, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnInvokeActivityAsync_ShouldCatchInternalServerError()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/setPropertyPaneConfiguration",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new ErrorActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(500, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnSharePointTaskGetCardViewAsync_ShouldBeCalled()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getCardView",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskGetCardViewAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnSharePointTaskGetQuickViewAsync_ShouldBeCalled()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getQuickView",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskGetQuickViewAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnSharePointTaskGetPropertyPaneConfigurationAsync_ShouldBeCalled()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getPropertyPaneConfiguration",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskGetPropertyPaneConfigurationAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnSharePointTaskSetPropertyPaneConfigurationAsync_ShouldBeCalled()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/setPropertyPaneConfiguration",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskSetPropertyPaneConfigurationAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnSharePointTaskHandleActionAsync_ShouldBeCalled()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/handleAction",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskHandleActionAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnSignInInvokeAsync_ShouldBeCalled()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/token",
                Value = new JsonObject()
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        private class TestActivityHandler : SharePointActivityHandler
        {
            public List<string> Record { get; } = [];

            // Invoke
            protected override Task<CardViewResponse> OnSharePointTaskGetCardViewAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(new CardViewResponse());
            }

            protected override Task<GetPropertyPaneConfigurationResponse> OnSharePointTaskGetPropertyPaneConfigurationAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(new GetPropertyPaneConfigurationResponse());
            }

            protected override Task<QuickViewResponse> OnSharePointTaskGetQuickViewAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(new QuickViewResponse());
            }

            protected override Task<BaseHandleActionResponse> OnSharePointTaskSetPropertyPaneConfigurationAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult<BaseHandleActionResponse>(new NoOpHandleActionResponse());
            }

            protected override Task<BaseHandleActionResponse> OnSharePointTaskHandleActionAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult<BaseHandleActionResponse>(new NoOpHandleActionResponse());
            }

            protected override Task OnSignInInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(true);
            }
        }

        private class ErrorActivityHandler : SharePointActivityHandler
        {
            public List<string> Record { get; } = [];

            protected override Task<BaseHandleActionResponse> OnSharePointTaskSetPropertyPaneConfigurationAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult<BaseHandleActionResponse>(new QuickViewHandleActionResponse());
            }
        }
    }
}
