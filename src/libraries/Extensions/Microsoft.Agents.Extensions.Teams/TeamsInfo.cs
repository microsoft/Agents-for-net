// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Api.Clients;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="meetingId">The id of the Teams meeting. ChannelData.Meeting.Id will be used if none provided.</param>
    /// <param name="participantId">The id of the Teams meeting participant. From.AadObjectId will be used if none provided.</param>
    /// <param name="tenantId">The id of the Teams meeting Tenant. ChannelData.Tenant.Id will be used if none provided.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks> <see cref="InvalidOperationException"/> will be thrown if meetingId, participantId or tenantId have not been
    /// provided, and also cannot be retrieved from turnContext.Activity.</remarks>
    /// <returns>Team participant channel account.</returns>
    /// <exception cref="InvalidOperationException">Thrown when meetingId, participantId or tenantId are not provided and cannot be found on the turnContext.</exception>
    public static async Task<Microsoft.Teams.Api.Clients.MeetingParticipant> GetMeetingParticipantAsync(ITurnContext turnContext, string meetingId = null, string participantId = null, string tenantId = null, CancellationToken cancellationToken = default)
    {
        meetingId ??= turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of a MS Teams Meeting.");
        participantId ??= turnContext.Activity.From.AadObjectId ?? throw new InvalidOperationException($"{nameof(participantId)} is required.");
        tenantId ??= turnContext.Activity.GetChannelData<Microsoft.Teams.Api.ChannelData>()?.Tenant?.Id ?? throw new InvalidOperationException($"{nameof(tenantId)} is required.");

        return await GetTeamsApiClient(turnContext).Meetings.GetParticipantAsync(meetingId, participantId, tenantId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the information for the given meeting id.
    /// </summary>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="meetingId"> The id of the Teams meeting. If null and the turnContext is within the scope of a Teams meeting, the meetingId will be inferred from the ChannelData.</param>
    /// <param name="cancellationToken"> Cancellation token.</param>
    /// <returns>Team Details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when meetingId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<Microsoft.Teams.Api.Meetings.Meeting> GetMeetingInfoAsync(ITurnContext turnContext, string meetingId = null, CancellationToken cancellationToken = default)
    {
        meetingId ??= turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("The meetingId can only be null if turnContext is within the scope of a MS Teams Meeting.");
        return await GetTeamsApiClient(turnContext).Meetings.GetByIdAsync(meetingId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the details for the given team id. This only works in teams scoped conversations. 
    /// </summary>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="teamId">ID of the Teams team. If null and the turnContext is within the scope of a Teams conversation, the teamId will be inferred from the ChannelData.</param>
    /// <param name="cancellationToken"> Cancellation token. </param>
    /// <returns>Team Details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<Microsoft.Teams.Api.Team> GetTeamDetailsAsync(ITurnContext turnContext, string teamId = null, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        return await GetTeamsApiClient(turnContext).Teams.GetByIdAsync(teamId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a list of channels in a Team.
    /// This only works in teams scoped conversations.
    /// </summary>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="teamId">ID of the Teams team. If null and the turnContext is within the scope of a Teams conversation, the teamId will be inferred from the ChannelData.</param>
    /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>A list of channels belonging to the specified team.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<IList<Microsoft.Teams.Api.Channel>> GetTeamChannelsAsync(ITurnContext turnContext, string teamId = null, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        return await GetTeamsApiClient(turnContext).Teams.GetConversationsAsync(teamId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a paginated list of members of one-on-one, group, or team conversation.
    /// </summary>
    /// <remarks>For Activities containing the Teams ChannelData with Team information, the ChannelData.Team.Id is used to determine the team context.  Otherwise
    /// the Activity.Conversation.Id is used.</remarks>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="pageSize"> Suggested number of entries on a page. </param>
    /// <param name="continuationToken"> ContinuationToken token. </param>
    /// /// <param name="cancellationToken"> Cancellation token. </param>
    /// <returns>PagedMembersResult.</returns>
    public static async Task<PagedMembersResult> GetPagedMembersAsync(ITurnContext turnContext, int? pageSize = default, string continuationToken = default, CancellationToken cancellationToken = default)
    {
        var teamInfo = turnContext.Activity.TeamsGetTeamInfo();

        if (teamInfo?.Id != null)
        {
            return await GetPagedTeamMembersAsync(turnContext, teamInfo.Id, pageSize, continuationToken, cancellationToken);
        }
        else
        {
            var conversationId = turnContext.Activity?.Conversation?.Id;
            var corePagedResult = await GetConnectorClient(turnContext).Conversations.GetConversationPagedMembersAsync(conversationId, pageSize, continuationToken, cancellationToken).ConfigureAwait(false);
            return new PagedMembersResult
            {
                ContinuationToken = corePagedResult.ContinuationToken,
                Members = [.. corePagedResult.Members.Select(m => m.ToTeamsAccount())],
            };
        }
    }

    /// <summary>
    /// Gets a paginated list of members of a team. 
    /// This only works in teams scoped conversations.
    /// </summary>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="teamId"> ID of the Teams team. </param>
    /// <param name="continuationToken"> continuationToken token. </param>
    /// <param name="pageSize"> number of entries on the page. </param>
    /// /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>PagedMembersResult.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext. </exception>
    public static Task<PagedMembersResult> GetPagedTeamMembersAsync(ITurnContext turnContext, string teamId = null, int? pageSize = default, string continuationToken = default, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        return GetTeamsApiClient(turnContext).Conversations.Members.GetPagedAsync(teamId, pageSize, continuationToken, cancellationToken);
    }

    /// <summary>
    /// Gets the member of a teams scoped conversation.
    /// </summary>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="userId"> user id. </param>
    /// <param name="teamId">ID of the Teams team. If null, then for Teams scoped conversations, the Activity.ChannelData.Team.Id is used.</param>
    /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>A <see cref="Microsoft.Teams.Api.Account"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static Task<Microsoft.Teams.Api.Account> GetTeamMemberAsync(ITurnContext turnContext, string userId, string teamId = null, CancellationToken cancellationToken = default)
    {
        teamId ??= turnContext.Activity.TeamsGetTeamInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of MS Teams Team.");
        return GetTeamsApiClient(turnContext).Conversations.Members.GetByIdAsync(teamId, userId, cancellationToken);
    }

    /// <summary>
    /// Gets the account of a single conversation member. 
    /// </summary>
    /// <remarks>    
    /// This works in one-on-one, group, and teams scoped conversations.  To ease multi-channel development, for Teams the ChannelData.Team.Id will be used
    /// and <see cref="GetTeamMemberAsync"/> will be called.  For other channels, the Conversation.Id will be used and the ConnectorClient 
    /// Conversation.GetConversationMemberAsync will be called directly."/>
    /// </remarks>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="userId"> ID of the user in question. </param>
    /// <param name="cancellationToken"> cancellation token. </param>
    /// <returns>A <see cref="Microsoft.Teams.Api.Account"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when teamId is not provided and cannot be found on the turnContext.</exception>
    public static async Task<Microsoft.Teams.Api.Account> GetMemberAsync(ITurnContext turnContext, string userId, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(userId, nameof(userId));

        var teamInfo = turnContext.Activity.TeamsGetTeamInfo();
        if (teamInfo?.Id != null)
        {
            return await GetTeamMemberAsync(turnContext, userId, teamInfo.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var conversationId = turnContext.Activity?.Conversation?.Id;
            var channelAccount = await GetConnectorClient(turnContext).Conversations.GetConversationMemberAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
            return channelAccount.ToTeamsAccount();
        }
    }

    private static IConnectorClient GetConnectorClient(ITurnContext turnContext)
    {
        return turnContext.Services.Get<IConnectorClient>() ?? throw new InvalidOperationException("IConnectorClient is not available.  Was IChannelServiceClientFactory registered?");
    }

    private static Microsoft.Teams.Api.Clients.ApiClient GetTeamsApiClient(ITurnContext turnContext)
    {
        return turnContext.GetTeamsApiClient() ?? throw new InvalidOperationException("Teams ApiClient is not available.");
    }
}
