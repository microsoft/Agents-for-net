// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2ATurnContextTests
{
    [Fact]
    public void A2AActivity_ChannelData_IsStronglyTyped()
    {
        var message = new Message { MessageId = "m1", Role = Role.User };
        var activity = new A2AMessageActivity { Type = ActivityTypes.Message, ChannelId = Channels.A2A, ChannelData = message };

        Assert.Same(message, activity.ChannelData);
        Assert.Same(message, ((IActivity)activity).ChannelData);
    }

    [Fact]
    public void ActivityFromMessage_ProducesTypedA2AActivity()
    {
        var message = new Message
        {
            MessageId = "m1",
            Role = Role.User,
            Parts = [new Part { Text = "hello" }]
        };

        var activity = A2AActivity.ActivityFromMessage("req1", "task1", message);

        var typed = Assert.IsType<A2AMessageActivity>(activity);
        Assert.IsAssignableFrom<IA2AActivity>(typed);
        Assert.Same(message, typed.ChannelData);
    }

    [Fact]
    public void A2ATurnContext_ExposesTypedActivity()
    {
        var message = new Message { MessageId = "m1", Role = Role.User };
        var activity = new A2AMessageActivity { Type = ActivityTypes.Message, ChannelId = Channels.A2A, ChannelData = message };

        var inner = new Mock<ITurnContext>();
        inner.Setup(c => c.Activity).Returns(activity);

        var context = new A2ATurnContext(inner.Object);

        Assert.IsAssignableFrom<IA2AActivity>(context.Activity);
        Assert.Same(message, ((IA2ATurnContext)context).Activity.ChannelData);
        Assert.Same(activity, ((ITurnContext)context).Activity);
    }

    [Fact]
    public void A2ATurnContext_Client_IsNotNull()
    {
        var activity = new A2AMessageActivity { Type = ActivityTypes.Message, ChannelId = Channels.A2A };
        var inner = new Mock<ITurnContext>();
        inner.Setup(c => c.Activity).Returns(activity);
        inner.SetupGet(c => c.Services).Returns(new TurnContextStateCollection());

        var context = new A2ATurnContext(inner.Object);

        Assert.NotNull(context.Client);
    }
}
