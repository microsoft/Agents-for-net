// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Clients;
using Microsoft.Teams.Api.Meetings;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Helper methods for accessing Teams information such as team details, meeting details, and members. These methods will attempt to infer necessary information 
/// from the turnContext when not provided, but will throw exceptions when required information cannot be found.
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
    /// <exception cref="InvalidOperationException">Thrown when meetingId, participantId or tenantId are not provided and cannot be found on the turnContext.</exception>
    public static async Task<MeetingParticipant> GetMeetingParticipantAsync(ITurnContext turnContext, string meetingId = null, string participantId = null, string tenantId = null, CancellationToken cancellationToken = default)
    {
        meetingId ??= turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of a MS Teams Meeting.");
        participantId ??= turnContext.Activity.From.AadObjectId ?? throw new InvalidOperationException($"{nameof(participantId)} is required.");
        tenantId ??= turnContext.Activity.GetChannelData<ChannelData>()?.Tenant?.Id ?? throw new InvalidOperationException($"{nameof(tenantId)} is required.");

        var teamsClient = GetTeamsApiClient(turnContext);
        return await teamsClient.Meetings.GetParticipantAsync(meetingId, participantId, tenantId, cancellationToken);
    }

    /// <summary>
    /// Gets the information for the given meeting id.
    /// </summary>
    /// <param name="turnContext"> Turn context.</param>
    /// <param name="meetingId"> The id of the Teams meeting. If null and the turnContext is within the scope of a Teams meeting, the meetingId will be inferred from the ChannelData.</param>
    /// <param name="cancellationToken"> Cancellation token.</param>
    /// <returns>Team Details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when meetingId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<Meeting> GetMeetingInfoAsync(ITurnContext turnContext, string meetingId = null, CancellationToken cancellationToken = default)
    {
        meetingId ??= turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("The meetingId can only be null if turnContext is within the scope of a MS Teams Meeting.");
        var teamsClient = GetTeamsApiClient(turnContext);
        return await teamsClient.Meetings.GetByIdAsync(meetingId, cancellationToken);
    }

    /// <summary>
    /// Gets the details for the given team id. This only works in teams scoped conversations. 
    /// </summary>
    /// <param name="turnContext"> Turn context. </param>
    /// <param name="teamId">ID of the Teams team. If null and the turnContext is within the scope of a Teams covnersation, the teamId will be inferred from the ChannelData.</param>
    /// <param name="cancellationToken"> Cancellation token. </param>
    /// <returns>Team Details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<Team> GetTeamDetailsAsync(ITurnContext turnContext, string teamId = null, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        var teamsClient = GetTeamsApiClient(turnContext);
        return await teamsClient.Teams.GetByIdAsync(teamId, cancellationToken);
    }

    /// <summary>
    /// Returns a list of channels in a Team. 
    /// This only works in teams scoped conversations.
    /// </summary>
    /// <param name="turnContext"> Turn context. </param>
    /// <param name="teamId">ID of the Teams team. If null and the turnContext is within the scope of a Teams covnersation, the teamId will be inferred from the ChannelData.</param>
    /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>Team Details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<IList<Channel>> GetTeamChannelsAsync(ITurnContext turnContext, string teamId = null, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        var teamsClient = GetTeamsApiClient(turnContext);
        return await teamsClient.Teams.GetConversationsAsync(teamId, cancellationToken);
    }

    /// <summary>
    /// Gets a paginated list of members of one-on-one, group, or team conversation.
    /// </summary>
    /// <remarks>For Activities containing the Teams ChannelData with Team information, the ChannelData.Team.Id is used to determine the team context.  Otherwise
    /// the Activity.Conversation.Id is used.</remarks>
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
            return GetPagedTeamMembersAsync(turnContext, teamInfo.Id, pageSize, continuationToken, cancellationToken);
        }
        else
        {
            var conversationId = turnContext.Activity?.Conversation?.Id;
            return GetPagedMembersAsync(GetConnectorClient(turnContext), conversationId, pageSize, continuationToken, cancellationToken);
        }
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
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext. </exception>
    public static Task<TeamsPagedMembersResult> GetPagedTeamMembersAsync(ITurnContext turnContext, string teamId = null, int? pageSize = default, string continuationToken = default, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        return GetPagedMembersAsync(GetConnectorClient(turnContext), teamId, pageSize, continuationToken, cancellationToken);
    }

    /// <summary>
    /// Gets the member of a teams scoped conversation.
    /// </summary>
    /// <param name="turnContext"> Turn context. </param>
    /// <param name="userId"> user id. </param>
    /// <param name="teamId">ID of the Teams team. If null, then for Teams scoped conversations, the Activity.ChannelData.Team.Id is used.</param>
    /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>The member <c>Account</c></returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static Task<Account> GetTeamMemberAsync(ITurnContext turnContext, string userId, string teamId = null, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        return GetMemberAsync(GetConnectorClient(turnContext), userId, teamId, cancellationToken);
    }

    /// <summary>
    /// Gets the account of a single conversation member. 
    /// </summary>
    /// <remarks>    
    /// This works in one-on-one, group, and teams scoped conversations.  To ease multi-channel development, for Teams the ChannelData.Team.Id will be used
    /// and <see cref="GetTeamMemberAsync"/> will be called.  For other channels, the Conversation.Id will be used and the ConnectorClient 
    /// Conversation.GetConversationMemberAsync will be called directly."/>
    /// </remarks>
    /// <param name="turnContext"> Turn context. </param>
    /// <param name="userId"> ID of the user in question. </param>
    /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>The member <c>Account</c></returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
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

    private static async Task<TeamsPagedMembersResult> GetPagedMembersAsync(IConnectorClient connectorClient, string conversationId, int? pageSize = default, string continuationToken = default, CancellationToken cancellationToken = default)
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
