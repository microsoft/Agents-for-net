// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Teams.Apps.Meetings;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.Meetings;

/// <summary>
/// Function for handling Microsoft Teams meeting start events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="meeting">The details of the meeting.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task MeetingStartHandler(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Clients.MeetingDetails meeting, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Microsoft Teams meeting end events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="meeting">The details of the meeting.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task MeetingEndHandler(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Clients.MeetingDetails meeting, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Microsoft Teams meeting participants join events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="meeting">The details of the meeting.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task MeetingParticipantsJoinHandler(ITeamsTurnContext turnContext, ITurnState turnState, MeetingParticipantJoinValue meeting, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Microsoft Teams meeting participants leave events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="meeting">The details of the participants who left the meeting.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task MeetingParticipantsLeaveHandler(ITeamsTurnContext turnContext, ITurnState turnState, MeetingParticipantLeaveValue meeting, CancellationToken cancellationToken);