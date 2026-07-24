// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.App;

/// <summary>
/// Represents a Teams-aware handler for handoff activities.
/// </summary>
/// <param name="turnContext">A Teams-specific turn context for the current turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="continuation">The continuation token.</param>
/// <param name="cancellationToken">A cancellation token that can be used to observe cancellation.</param>
/// <returns>A task that represents the asynchronous handler operation.</returns>
public delegate Task TeamsHandoffHandler(ITeamsTurnContext turnContext, ITurnState turnState, string continuation, CancellationToken cancellationToken);