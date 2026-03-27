// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.TeamsChannels;

/// <summary>
/// Represents a delegate that handles channel update events in Microsoft Teams, processing the provided context and
/// channel data asynchronously.
/// </summary>
/// <remarks>Use this delegate to implement custom logic in response to channel update events, such as when a
/// channel is created, renamed, or deleted in Microsoft Teams.</remarks>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="data">The channel data associated with the update event, containing details about the Microsoft Teams channel that
/// triggered the event.</param>
/// <param name="cancellationToken">A cancellation token that can be used to request cancellation of the asynchronous operation.</param>
/// <returns>A task that represents the asynchronous operation of handling the channel update event.</returns>
public delegate Task ChannelUpdateHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Channel data, CancellationToken cancellationToken);
