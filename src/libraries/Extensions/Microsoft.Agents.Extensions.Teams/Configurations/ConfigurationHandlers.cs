// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Configurations;

/// <summary>
/// Function for handling config events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="configData">The config data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A <see cref="Microsoft.Teams.Api.Config.ConfigResponse"/>.</returns>
public delegate Task<Microsoft.Teams.Api.Config.ConfigResponse> ConfigurationHandler(ITurnContext turnContext, ITurnState turnState, object configData, CancellationToken cancellationToken);
