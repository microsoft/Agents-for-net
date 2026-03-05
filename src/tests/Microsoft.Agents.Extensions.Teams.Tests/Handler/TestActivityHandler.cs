// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Activities.Events;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    internal class TestActivityHandler : TeamsActivityHandler
    {

        public List<string> Record { get; } = [];

        // ConversationUpdate
        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelCreatedAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelCreatedAsync(Channel, Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelDeletedAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelDeletedAsync(Channel, Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelRenamedAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelRenamedAsync(Channel, Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelRestoredAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelRestoredAsync(Channel, Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamArchivedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamArchivedAsync(Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamDeletedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamDeletedAsync(Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamHardDeletedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamHardDeletedAsync(Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamRenamedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamRenamedAsync(Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamRestoredAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamRestoredAsync(Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamUnarchivedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamUnarchivedAsync(Team, turnContext, cancellationToken);
        }

        protected override Task OnTeamsMembersAddedAsync(IList<Account> membersAdded, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsMembersRemovedAsync(IList<Account> membersRemoved, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnMembersRemovedAsync(IList<ChannelAccount> membersRemoved, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsReadReceiptAsync(ReadReceiptInfo readReceiptInfo, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(readReceiptInfo.LastReadMessageId);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsMeetingParticipantsJoinAsync(MeetingParticipantJoinActivityValue meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(meeting.Members[0].User.Id);
            return base.OnTeamsMeetingParticipantsJoinAsync(meeting, turnContext, cancellationToken);
        }

        protected override Task OnTeamsMeetingParticipantsLeaveAsync(MeetingParticipantLeaveActivityValue meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(meeting.Members[0].User.Id);
            return base.OnTeamsMeetingParticipantsLeaveAsync(meeting, turnContext, cancellationToken);
        }

        // Invoke
        protected override Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnInvokeActivityAsync(turnContext, cancellationToken);
        }

        protected override Task<InvokeResponse> OnTeamsFileConsentAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsFileConsentAsync(turnContext, fileConsentCardResponse, cancellationToken);
        }

        protected override Task OnTeamsFileConsentAcceptAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsFileConsentDeclineAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsO365ConnectorCardActionAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.O365.ConnectorCardActionQuery query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionAgentMessagePreviewEditAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionAgentMessagePreviewSendAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
        }

        protected override Task OnTeamsMessagingExtensionCardButtonClickedAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement cardData, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessagingExtensionCardButtonClickedAsync(turnContext, cardData, cancellationToken);
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionFetchTaskAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }

        protected override Task OnTeamsMessagingExtensionConfigurationSettingAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement settings, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionSubmitActionAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionSubmitActionDispatchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessagingExtensionSubmitActionDispatchAsync(turnContext, action, cancellationToken);
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedQueryLink query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsAnonymousAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedQueryLink query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }

        protected override Task<InvokeResponse> OnTeamsCardActionInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsCardActionInvokeAsync(turnContext, cancellationToken);
        }

        protected override Task<Microsoft.Teams.Api.TaskModules.Response> OnTeamsTaskModuleFetchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.TaskModules.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        protected override Task<Microsoft.Teams.Api.TaskModules.Response> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.TaskModules.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        protected override Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task<Microsoft.Teams.Api.Tabs.Response> OnTeamsTabFetchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.Tabs.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.Tabs.Response(new()));
        }

        protected override Task<Microsoft.Teams.Api.Tabs.Response> OnTeamsTabSubmitAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.Tabs.Submit taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new Microsoft.Teams.Api.Tabs.Response(new()));
        }

        protected override Task<Microsoft.Teams.Api.Config.ConfigResponse> OnTeamsConfigFetchAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement configData, CancellationToken cancellationToken)
        {
            Microsoft.Teams.Api.Config.ConfigResponse configResponse = new Microsoft.Teams.Api.Config.ConfigResponse();
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(configResponse);
        }

        protected override Task<Microsoft.Teams.Api.Config.ConfigResponse> OnTeamsConfigSubmitAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement configData, CancellationToken cancellationToken)
        {
            Microsoft.Teams.Api.Config.ConfigResponse configResponse = new Microsoft.Teams.Api.Config.ConfigResponse();
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(configResponse);
        }

        protected override Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnEventActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMeetingStartAsync(Microsoft.Teams.Api.Meetings.MeetingDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(new Activity() { Text = meeting.Title, Type = ActivityTypes.Message, Value = meeting.ScheduledStartTime });
            return Task.CompletedTask;
        }

        protected override Task OnTeamsMeetingEndAsync(Microsoft.Teams.Api.Meetings.MeetingDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(new Activity() { Text = meeting.Title, Type = ActivityTypes.Message, Value = meeting.ScheduledEndTime });
            return Task.CompletedTask;
        }

        protected override Task OnMessageUpdateActivityAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnMessageUpdateActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMessageEditAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessageEditAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMessageUndeleteAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessageUndeleteAsync(turnContext, cancellationToken);
        }

        protected override Task OnMessageDeleteActivityAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnMessageDeleteActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMessageSoftDeleteAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessageSoftDeleteAsync(turnContext, cancellationToken);
        }
    }
}
