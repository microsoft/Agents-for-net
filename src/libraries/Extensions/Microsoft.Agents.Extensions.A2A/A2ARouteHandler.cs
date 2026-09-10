// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Represents an A2A-aware route handler for an <c>AgentApplication</c> route.
/// </summary>
/// <param name="turnContext">An A2A-specific turn context for the current turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="cancellationToken">A cancellation token that can be used to observe cancellation.</param>
/// <returns>A task that represents the asynchronous handler operation.</returns>
public delegate Task A2ARouteHandler(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);
