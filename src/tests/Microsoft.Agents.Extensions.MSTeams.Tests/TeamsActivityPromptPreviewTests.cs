// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.Models;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests;

public class TeamsActivityPromptPreviewTests
{
    [Fact]
    public void AddQuotedReply_AddsEntityAndEscapedPlaceholder()
    {
        ITeamsActivity activity = new TeamsActivity
        {
            Type = ActivityTypes.Message,
            Text = string.Empty
        };

        activity.AddQuotedReply("message&\"id", "response");

        var quotedReply = Assert.Single(activity.GetQuotedMessages());
        Assert.Equal("message&\"id", quotedReply.QuotedReply.MessageId);
        Assert.Equal("<quoted messageId=\"message&amp;&quot;id\"/> response", activity.Text);
    }

    [Fact]
    public void TargetedMessageInfo_RoundTripsThroughProtocolSerializer()
    {
        Entity entity = new TargetedMessageInfoEntity { MessageId = "inbound-message" };

        var json = ProtocolJsonSerializer.ToJson(entity);
        var result = ProtocolJsonSerializer.ToObject<Entity>(json);

        var targetedMessageInfo = Assert.IsType<TargetedMessageInfoEntity>(result);
        Assert.Equal("inbound-message", targetedMessageInfo.MessageId);
    }

    [Fact]
    public void QuotedReply_RoundTripsThroughProtocolSerializer()
    {
        Entity entity = new QuotedReplyEntity
        {
            QuotedReply = new QuotedReplyData
            {
                MessageId = "quoted-message",
                SenderId = "sender",
                SenderName = "Sender",
                Preview = "preview",
                Time = "1772050244572",
                IsReplyDeleted = false,
                ValidatedMessageReference = true
            }
        };

        var json = ProtocolJsonSerializer.ToJson(entity);
        var result = ProtocolJsonSerializer.ToObject<Entity>(json);

        var quotedReply = Assert.IsType<QuotedReplyEntity>(result);
        Assert.Equal("quoted-message", quotedReply.QuotedReply.MessageId);
        Assert.Equal("sender", quotedReply.QuotedReply.SenderId);
        Assert.Equal("Sender", quotedReply.QuotedReply.SenderName);
        Assert.Equal("preview", quotedReply.QuotedReply.Preview);
        Assert.Equal("1772050244572", quotedReply.QuotedReply.Time);
        Assert.False(quotedReply.QuotedReply.IsReplyDeleted);
        Assert.True(quotedReply.QuotedReply.ValidatedMessageReference);
    }

    [Fact]
    public void IsRecipientTargeted_ReadsTeamsExtensionProperty()
    {
        ITeamsActivity activity = new TeamsActivity
        {
            Type = ActivityTypes.Message,
            Recipient = new ChannelAccount
            {
                Properties =
                {
                    ["isTargeted"] = JsonSerializer.SerializeToElement(true)
                }
            }
        };

        Assert.True(activity.IsRecipientTargeted());
    }

    [Fact]
    public void AddTargetedMessageInfo_AddsRetrievableEntity()
    {
        ITeamsActivity activity = new TeamsActivity { Type = ActivityTypes.Message };

        activity.AddTargetedMessageInfo("inbound-message");

        Assert.Equal("inbound-message", activity.GetTargetedMessageInfo().MessageId);
    }

    [Fact]
    public void AddTargetedMessageInfo_ExistingEntityIsNotDuplicated()
    {
        ITeamsActivity activity = new TeamsActivity { Type = ActivityTypes.Message };
        activity.AddTargetedMessageInfo("first-message");

        activity.AddTargetedMessageInfo("second-message");

        var targetedMessageInfo = Assert.Single(activity.Entities.OfType<TargetedMessageInfoEntity>());
        Assert.Equal("first-message", targetedMessageInfo.MessageId);
    }

    [Fact]
    public void AddTargetedMessageInfo_ExistingWireTypeIsNotDuplicated()
    {
        ITeamsActivity activity = new TeamsActivity
        {
            Type = ActivityTypes.Message,
            Entities = [new Entity(TargetedMessageInfoEntity.EntityName)]
        };

        activity.AddTargetedMessageInfo("second-message");

        Assert.Single(activity.Entities);
    }
}
