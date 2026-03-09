// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Clients;
using Microsoft.Teams.Api.Meetings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    /// <summary>
    /// The TeamsInfo Test If Build Remote Successful
    /// provides utility methods for the events and interactions that occur within Microsoft Teams.
    /// </summary>
    public static class TeamsInfo
    {
        /// <summary>
        /// Gets the details for the given meeting participant. This only works in teams meeting scoped conversations. 
        /// </summary>
        /// <param name="turnContext">Turn context.</param>
        /// <param name="meetingId">The id of the Teams meeting. ChannelData.Meeting.Id will be used if none provided.</param>
        /// <param name="participantId">The id of the Teams meeting participant. From.AadObjectId will be used if none provided.</param>
        /// <param name="tenantId">The id of the Teams meeting Tenant. ChannelData.Tenant.Id will be used if none provided.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks> <see cref="InvalidOperationException"/> will be thrown if meetingId, participantId or tenantId have not been
        /// provided, and also cannot be retrieved from turnContext.Activity.</remarks>
        /// <returns>Team participant channel account.</returns>
        public static async Task<MeetingParticipant> GetMeetingParticipantAsync(ITurnContext turnContext, string meetingId = null, string participantId = null, string tenantId = null, CancellationToken cancellationToken = default)
        {
            meetingId ??= turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of a MS Teams Meeting.");
            participantId ??= turnContext.Activity.From.AadObjectId ?? throw new InvalidOperationException($"{nameof(participantId)} is required.");
            tenantId ??= turnContext.Activity.GetChannelData<ChannelData>()?.Tenant?.Id ?? throw new InvalidOperationException($"{nameof(tenantId)} is required.");

            // Teams SDK 2.0.5 doesn't support supplying tenantId.
            var teamsClient = GetTeamsApiClient(turnContext);
            return await teamsClient.Meetings.GetParticipantAsync(meetingId, participantId);
        }

        /// <summary>
        /// Gets the information for the given meeting id.
        /// </summary>
        /// <param name="turnContext"> Turn context.</param>
        /// <param name="meetingId"> The BASE64-encoded id of the Teams meeting.</param>
        /// <param name="cancellationToken"> Cancellation token.</param>
        /// <returns>Team Details.</returns>
        public static async Task<Meeting> GetMeetingInfoAsync(ITurnContext turnContext, string meetingId = null, CancellationToken cancellationToken = default)
        {
            meetingId ??= turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("The meetingId can only be null if turnContext is within the scope of a MS Teams Meeting.");
            var teamsClient = GetTeamsApiClient(turnContext);
            return await teamsClient.Meetings.GetByIdAsync(meetingId);
        }

        /// <summary>
        /// Gets the details for the given team id. This only works in teams scoped conversations. 
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="teamId"> The id of the Teams team. </param>
        /// <param name="cancellationToken"> Cancellation token. </param>
        /// <returns>Team Details.</returns>
        public static async Task<Team> GetTeamDetailsAsync(ITurnContext turnContext, string teamId = null, CancellationToken cancellationToken = default)
        {
            teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
            var teamsClient = GetTeamsApiClient(turnContext);
            return await teamsClient.Teams.GetByIdAsync(teamId);
        }

        /// <summary>
        /// Returns a list of channels in a Team. 
        /// This only works in teams scoped conversations.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="teamId"> ID of the Teams team. </param>
        /// <param name="cancellationToken"> cancellation token. </param>
        /// <returns>Team Details.</returns>
        public static async Task<IList<Channel>> GetTeamChannelsAsync(ITurnContext turnContext, string teamId = null, CancellationToken cancellationToken = default)
        {
            teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
            var teamsClient = GetTeamsApiClient(turnContext);
            return await teamsClient.Teams.GetConversationsAsync(teamId);
        }

        /// <summary>
        /// Gets a paginated list of members of a team. 
        /// This only works in teams scoped conversations.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="teamId"> ID of the Teams team. </param>
        /// <param name="continuationToken"> continuationToken token. </param>
        /// <param name="pageSize"> number of entries on the page. </param>
        /// /// <param name="cancellationToken"> cancellation token. </param>
        /// <returns>TeamsPagedMembersResult.</returns>
        public static Task<TeamsPagedMembersResult> GetPagedTeamMembersAsync(ITurnContext turnContext, string teamId = null, string continuationToken = default, int? pageSize = default, CancellationToken cancellationToken = default)
        {
            teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
            return GetPagedMembersAsync(GetConnectorClient(turnContext), teamId, continuationToken, cancellationToken, pageSize);
        }

        /// <summary>
        /// Gets a paginated list of members of one-on-one, group, or team conversation.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="pageSize"> Suggested number of entries on a page. </param>
        /// <param name="continuationToken"> ContinuationToken token. </param>
        /// /// <param name="cancellationToken"> Cancellation token. </param>
        /// <returns>TeamsPagedMembersResult.</returns>
        public static Task<TeamsPagedMembersResult> GetPagedMembersAsync(ITurnContext turnContext, int? pageSize = default, string continuationToken = default, CancellationToken cancellationToken = default)
        {
            var teamInfo = turnContext.Activity.TeamsGetTeamInfo();

            if (teamInfo?.Id != null)
            {
                return GetPagedTeamMembersAsync(turnContext, teamInfo.Id, continuationToken, pageSize, cancellationToken);
            }
            else
            {
                var conversationId = turnContext.Activity?.Conversation?.Id;
                return GetPagedMembersAsync(GetConnectorClient(turnContext), conversationId, continuationToken, cancellationToken, pageSize);
            }
        }

        /// <summary>
        /// Gets the member of a teams scoped conversation.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="userId"> user id. </param>
        /// <param name="teamId"> ID of the Teams team. </param>
        /// <param name="cancellationToken"> cancellation token. </param>
        /// <returns>Team Details.</returns>
        public static Task<Account> GetTeamMemberAsync(ITurnContext turnContext, string userId, string teamId = null, CancellationToken cancellationToken = default)
        {
            teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
            return GetMemberAsync(GetConnectorClient(turnContext), userId, teamId, cancellationToken);
        }

        /// <summary>
        /// Gets the account of a single conversation member. 
        /// This works in one-on-one, group, and teams scoped conversations.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="userId"> ID of the user in question. </param>
        /// <param name="cancellationToken"> cancellation token. </param>
        /// <returns>Team Details.</returns>
        public static Task<Account> GetMemberAsync(ITurnContext turnContext, string userId, CancellationToken cancellationToken = default)
        {
            var teamInfo = turnContext.Activity.TeamsGetTeamInfo();
            if (teamInfo?.Id != null)
            {
                return GetTeamMemberAsync(turnContext, userId, teamInfo.Id, cancellationToken);
            }
            else
            {
                var conversationId = turnContext.Activity?.Conversation?.Id;
                return GetMemberAsync(GetConnectorClient(turnContext), userId, conversationId, cancellationToken);
            }
        }

        /// <summary>
        /// Creates a new thread in a team chat and sends an activity to that new thread. Use this method if you are using CloudAdapter where credentials are handled by the adapter.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="activity"> The activity to send on starting the new thread. </param>
        /// <param name="teamsChannelId"> The Team's Channel ID, note this is distinct from the Activity property with same name. </param>
        /// <param name="agentAppId"> The Agent's appId. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns>Team Details.</returns>
        public static async Task<Tuple<Core.Models.ConversationReference, string>> SendMessageToTeamsChannelAsync(ITurnContext turnContext, IActivity activity, string teamsChannelId, string agentAppId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            if (turnContext.Activity == null)
            {
                throw new InvalidOperationException(nameof(turnContext.Activity));
            }

            if (string.IsNullOrEmpty(teamsChannelId))
            {
                throw new ArgumentNullException(nameof(teamsChannelId));
            }

            Core.Models.ConversationReference conversationReference = null;
            var newActivityId = string.Empty;
            var serviceUrl = turnContext.Activity.ServiceUrl;
            var conversationParameters = new ConversationParameters
            {
                IsGroup = true,
                ChannelData = new ChannelData { Channel = new Channel() { Id = teamsChannelId } },
                Activity = (Activity)activity,
            };

            await turnContext.Adapter.CreateConversationAsync(
                agentAppId,
                Channels.Msteams,
                serviceUrl,
                null,
                conversationParameters,
                (t, ct) =>
                {
                    conversationReference = t.Activity.GetConversationReference();
                    newActivityId = t.Activity.Id;
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);

            return new Tuple<Core.Models.ConversationReference, string>(conversationReference, newActivityId);
        }

        #region No Direct Teams API Equivalent
        /// <summary>
        /// Sends a notification to meeting participants. This functionality is available only in teams meeting scoped conversations. 
        /// </summary>
        /// <param name="turnContext">Turn context.</param>
        /// <param name="notification">The notification to send to Teams.</param>
        /// <param name="meetingId">The id of the Teams meeting. TeamsChannelData.Meeting.Id will be used if none provided.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>InvalidOperationException will be thrown if meetingId or notification have not been
        /// provided, and also cannot be retrieved from turnContext.Activity.</remarks>
        /// <returns> <see cref="MeetingNotificationResponse"/>.</returns>
        public static Task<object> SendMeetingNotificationAsync(ITurnContext turnContext, object notification, string meetingId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message to the provided list of Teams members.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="activity"> The activity to send. </param>
        /// <param name="teamsMembers"> The list of members. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> The operation Id. </returns>
        public static Task<string> SendMessageToListOfUsersAsync(ITurnContext turnContext, IActivity activity, List<Account> teamsMembers, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message to all the users in a tenant.
        /// </summary>
        /// <param name="turnContext"> The turn context. </param>
        /// <param name="activity"> The activity to send to the tenant. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> The operation Id. </returns>
        public static Task<string> SendMessageToAllUsersInTenantAsync(ITurnContext turnContext, IActivity activity, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message to all the users in a team.
        /// </summary>
        /// <param name="turnContext"> The turn context. </param>
        /// <param name="activity"> The activity to send to the users in the team. </param>
        /// <param name="teamId"> The team ID. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns>The operation Id.</returns>
        public static Task<string> SendMessageToAllUsersInTeamAsync(ITurnContext turnContext, IActivity activity, string teamId, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a message to the provided list of Teams channels.
        /// </summary>
        /// <param name="turnContext"> The turn context. </param>
        /// <param name="activity"> The activity to send. </param>
        /// <param name="channelsMembers"> The list of channels. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> The operation Id. </returns>
        public static Task<string> SendMessageToListOfChannelsAsync(ITurnContext turnContext, IActivity activity, List<object> channelsMembers, string tenantId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the state of an operation.
        /// </summary>
        /// <param name="turnContext"> Turn context. </param>
        /// <param name="operationId"> The operationId to get the state of. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> The state and responses of the operation. </returns>
        public static Task<object> GetOperationStateAsync(ITurnContext turnContext, string operationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the failed entries of a batch operation.
        /// </summary>
        /// <param name="turnContext"> The turn context. </param>
        /// <param name="operationId"> The operationId to get the failed entries of. </param>
        /// <param name="continuationToken"> The continuation token. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> The list of failed entries of the operation. </returns>
        public static Task<object> GetPagedFailedEntriesAsync(ITurnContext turnContext, string operationId, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cancels a batch operation by its id.
        /// </summary>
        /// <param name="turnContext"> The turn context. </param>
        /// <param name="operationId"> The id of the operation to cancel. </param>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> A <see cref="Task"/> representing the asynchronous operation. </returns>
        public static Task CancelOperationAsync(ITurnContext turnContext, string operationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        #endregion

        private static async Task<IEnumerable<Account>> GetMembersAsync(IConnectorClient connectorClient, string conversationId, CancellationToken cancellationToken)
        {
            if (conversationId == null)
            {
                throw new InvalidOperationException("The GetMembers operation needs a valid conversation Id.");
            }

            var teamMembers = await connectorClient.Conversations.GetConversationMembersAsync(conversationId, cancellationToken).ConfigureAwait(false);
            var teamsChannelAccounts = teamMembers.Select(channelAccount => ProtocolJsonSerializer.ToObject<Account>(channelAccount));
            return teamsChannelAccounts;
        }

        private static async Task<Account> GetMemberAsync(IConnectorClient connectorClient, string userId, string conversationId, CancellationToken cancellationToken)
        {
            if (conversationId == null)
            {
                throw new InvalidOperationException("The GetMembers operation needs a valid conversation Id.");
            }

            if (userId == null)
            {
                throw new InvalidOperationException("The GetMembers operation needs a valid user Id.");
            }

            // Use API client
            var channelAccount = await connectorClient.Conversations.GetConversationMemberAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
            return channelAccount.ToTeamsAccount();
        }

        private static async Task<TeamsPagedMembersResult> GetPagedMembersAsync(IConnectorClient connectorClient, string conversationId, string continuationToken, CancellationToken cancellationToken, int? pageSize = default)
        {
            if (conversationId == null)
            {
                throw new InvalidOperationException("The GetMembers operation needs a valid conversation Id.");
            }

            var corePagedResult = await connectorClient.Conversations.GetConversationPagedMembersAsync(conversationId, pageSize, continuationToken, cancellationToken).ConfigureAwait(false);
            return new TeamsPagedMembersResult(corePagedResult.ContinuationToken, corePagedResult.Members);
        }

        private static IConnectorClient GetConnectorClient(ITurnContext turnContext)
        {
            return turnContext.Services.Get<IConnectorClient>() ?? throw new InvalidOperationException("IConnectorClient is not available.  Was IChannelServiceClientFactory registered?");
        }

        private static ApiClient GetTeamsApiClient(ITurnContext turnContext)
        {
            return turnContext.GetTeamsApiClient() ?? throw new InvalidOperationException("Teams ApiClient is not available.");
        }
    }
}
