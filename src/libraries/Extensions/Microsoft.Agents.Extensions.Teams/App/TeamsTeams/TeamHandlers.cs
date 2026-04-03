// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.TeamsTeams;

/// <summary>
/// Represents a delegate that handles team update events in Microsoft Teams.
/// </summary>
/// <remarks>Use this delegate to implement custom logic in response to team update events, such as when a
/// team is created, renamed, or deleted in Microsoft Teams.</remarks>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="data">The data associated with the update event, containing details about the team that
/// triggered the event.</param>
/// <param name="cancellationToken">A cancellation token that can be used to request cancellation of the asynchronous operation.</param>
/// <returns>A task that represents the asynchronous operation of handling the team update event.</returns>
public delegate Task TeamUpdateHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team data, CancellationToken cancellationToken);