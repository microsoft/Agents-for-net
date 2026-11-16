// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests;

public class HttpProactiveTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UsesConversationChannelInsteadOfDefaultAdapter(bool continueConversation, bool stored)
    {
        var storage = new MemoryStorage();
        var channelAdapter = new RecordingAdapter();
        var defaultAdapter = new Mock<IChannelAdapter>(MockBehavior.Strict);
        var registry = new Mock<IChannelAdapterRegistry>();
        registry.Setup(r => r.GetDefault()).Returns(defaultAdapter.Object);
        IChannelAdapter resolved = channelAdapter;
        registry.Setup(r => r.TryGetAdapter("custom", out resolved)).Returns(true);
        var agent = new TestAgent(new AgentApplicationOptions(storage)
        {
            ChannelAdapterRegistry = registry.Object,
            Proactive = new ProactiveOptions(storage),
        });
        var conversation = new Conversation(new ClaimsIdentity(), new ConversationReference
        {
            ChannelId = "custom",
            Conversation = new ConversationAccount { Id = "conversation-1" },
            Agent = new ChannelAccount { Id = "agent" },
            User = new ChannelAccount { Id = "user" },
        });
        if (stored)
        {
            await agent.Proactive.StoreConversationAsync(conversation);
        }
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?message=continued");
        http.Response.Body = new MemoryStream();
        object body = continueConversation ? conversation
            : stored ? MessageFactory.Text("sent")
            : new SendToConversationBody { Conversation = conversation, Activity = MessageFactory.Text("sent") };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(body)));
        var logger = NullLogger<HttpProactive>.Instance;
        var route = new ContinueConversationRoute<TestAgent>(nameof(TestAgent.ContinueAsync), tokenHandlers: []);

        if (continueConversation && stored)
        {
            await HttpProactive.ContinueConversationWithConversationIdAsync(route, http.Request, http.Response, agent, "conversation-1", logger, default);
        }
        else if (continueConversation)
        {
            await HttpProactive.ContinueConversationWithConversationAsync(route, http.Request, http.Response, agent, logger, default);
        }
        else if (stored)
        {
            await HttpProactive.SendActivityWithConversationIdAsync(http.Request, http.Response, agent, "conversation-1", logger, default);
        }
        else
        {
            await HttpProactive.SendActivityWithConversationAsync(http.Request, http.Response, agent, logger, default);
        }

        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        Assert.Equal(continueConversation ? "continued" : "sent", Assert.Single(channelAdapter.Sent).Text);
        defaultAdapter.VerifyNoOtherCalls();
    }

    public sealed class TestAgent(AgentApplicationOptions options) : AgentApplication(options)
    {
        public Task ContinueAsync(ITurnContext context, ITurnState state, CancellationToken ct)
        {
            Assert.Equal(Proactive.ContinueConversationValueType, context.Activity.ValueType);
            var values = ProtocolJsonSerializer.ToObject<System.Collections.Generic.Dictionary<string, string>>(context.Activity.Value);
            return context.SendActivityAsync(values["message"], cancellationToken: ct);
        }
    }

    private sealed class RecordingAdapter : ChannelAdapter
    {
        public System.Collections.Generic.List<IActivity> Sent { get; } = [];

        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            Sent.AddRange(activities);
            return Task.FromResult(new[] { new ResourceResponse("activity-1") });
        }
    }
}
