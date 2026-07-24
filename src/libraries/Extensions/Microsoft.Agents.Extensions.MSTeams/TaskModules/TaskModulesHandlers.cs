// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.TaskModules;

/// <summary>
/// Function for handling Task Module fetch events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="request">The request data associated with the fetch.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
/// <returns>An instance of Microsoft.Teams.Api.TaskModules.Response.</returns>
public delegate Task<Microsoft.Teams.Api.TaskModules.Response> TaskFetchHandler(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Task Module submit events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="request">The request data associated with the submit.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
/// <returns>An instance of Microsoft.Teams.Api.TaskModules.Response.</returns>
public delegate Task<Microsoft.Teams.Api.TaskModules.Response> TaskSubmitHandler(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken);