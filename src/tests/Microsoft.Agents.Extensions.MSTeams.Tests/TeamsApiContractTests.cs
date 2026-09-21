using System;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Clients;
using Microsoft.Teams.Apps.Files;
using Microsoft.Teams.Apps.Meetings;
using Microsoft.Teams.Apps.MessageExtensions;
using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Apps.TaskModules;
using Microsoft.Teams.Apps.Utils;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests;

[Trait("Category", "TeamsApiContract")]
public sealed class TeamsApiContractTests
{
    [Fact]
    public void CuratedTeamsApiTypesAndMembersRemainCompileTimeAvailable()
    {
        Type[] types =
        [
            typeof(ApiClient), typeof(Meeting), typeof(MeetingDetails), typeof(ConversationEventType),
            typeof(EventName), typeof(EventNames), typeof(FileConsentValue), typeof(InvokeName), typeof(InvokeNames),
            typeof(MeetingParticipantJoinValue), typeof(MeetingParticipantLeaveValue), typeof(MessageActivity),
            typeof(BotMessagePreviewActionType), typeof(BotMessagePreviewActionTypes), typeof(ComposeExtension),
            typeof(MessageExtensionAction), typeof(MessageExtensionActionResponse), typeof(MessageExtensionActivityPreview),
            typeof(MessageExtensionQuery), typeof(MessageExtensionQueryLink), typeof(MessageExtensionResponse),
            typeof(MessageExtensionResponseType), typeof(MessageExtensionResponseTypes), typeof(MessageReaction),
            typeof(AttachmentContentType), typeof(Team), typeof(TeamsActivity), typeof(TeamsAttachment),
            typeof(TeamsChannel), typeof(TeamsChannelAccount), typeof(TeamsChannelData),
            typeof(TeamsChannelDataSettings), typeof(TaskModuleRequest), typeof(TaskModuleResponse), typeof(StringEnum)
        ];

        string[] members =
        [
            nameof(ConversationEventType.ChannelCreated), nameof(ConversationEventType.ChannelDeleted),
            nameof(ConversationEventType.ChannelMemberAdded), nameof(ConversationEventType.ChannelMemberRemoved),
            nameof(ConversationEventType.ChannelRenamed), nameof(ConversationEventType.ChannelRestored),
            nameof(ConversationEventType.ChannelShared), nameof(ConversationEventType.ChannelUnShared),
            nameof(ConversationEventType.TeamArchived), nameof(ConversationEventType.TeamDeleted),
            nameof(ConversationEventType.TeamRenamed), nameof(ConversationEventType.TeamRestored),
            nameof(ConversationEventType.TeamUnarchived), nameof(EventNames.MeetingEnd),
            nameof(EventNames.MeetingParticipantJoin), nameof(EventNames.MeetingParticipantLeave), nameof(EventNames.MeetingStart),
            nameof(FileConsentValue.Action), nameof(InvokeNames.FileConsent), nameof(InvokeNames.MessageExtensionAnonQueryLink),
            nameof(InvokeNames.MessageExtensionCardButtonClicked), nameof(InvokeNames.MessageExtensionFetchTask),
            nameof(InvokeNames.MessageExtensionQuery), nameof(InvokeNames.MessageExtensionQueryLink),
            nameof(InvokeNames.MessageExtensionQuerySettingUrl), nameof(InvokeNames.MessageExtensionSelectItem),
            nameof(InvokeNames.MessageExtensionSetting), nameof(InvokeNames.MessageExtensionSubmitAction),
            nameof(InvokeNames.TaskFetch), nameof(InvokeNames.TaskSubmit), nameof(MessageActivity.Text),
            nameof(BotMessagePreviewActionTypes.Edit), nameof(BotMessagePreviewActionTypes.Send),
            nameof(ComposeExtension.Text), nameof(ComposeExtension.Type), nameof(MessageExtensionAction.BotActivityPreview),
            nameof(MessageExtensionAction.Data), nameof(MessageExtensionResponse.ComposeExtension),
            nameof(MessageExtensionResponseTypes.Message), nameof(TeamsAttachment.Content), nameof(TeamsAttachment.ContentType),
            nameof(TeamsAttachment.ContentUrl), nameof(TeamsAttachment.Name), nameof(TeamsAttachment.Properties),
            nameof(TeamsAttachment.ThumbnailUrl), nameof(TeamsChannel.Id), nameof(TeamsChannelData.Channel),
            nameof(TeamsChannelData.EventType), nameof(TeamsChannelData.Settings), nameof(TeamsChannelData.Team),
            nameof(TeamsChannelDataSettings.SelectedChannel), nameof(TaskModuleRequest.Data)
        ];

        Assert.Equal(35, types.Length);
        Assert.All(types, type => Assert.NotNull(type.FullName));
        Assert.All(members, member => Assert.False(string.IsNullOrWhiteSpace(member)));
    }
}
