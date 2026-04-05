// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Teams.Api.Config;
using Microsoft.Teams.Api.O365;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Function for handling config events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="configData">The config data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of ConfigResponseBase.</returns>
public delegate Task<ConfigResponse> ConfigHandler(ITurnContext turnContext, ITurnState turnState, object configData, CancellationToken cancellationToken);

/// <summary>
/// Function for handling O365 Connector Card Action activities.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="query">The O365 connector card HttpPOST invoke query.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task O365ConnectorCardActionHandler(ITurnContext turnContext, ITurnState turnState, ConnectorCardActionQuery query, CancellationToken cancellationToken);

/// <summary>
/// Function for handling read receipt events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="data">The read receipt data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task ReadReceiptHandler(ITurnContext turnContext, ITurnState turnState, JsonElement data, CancellationToken cancellationToken);
