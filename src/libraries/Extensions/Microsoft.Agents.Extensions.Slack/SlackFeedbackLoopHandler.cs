// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// Represents a Slack-aware feedback loop handler for an <see cref="AgentApplication"/> route.
/// </summary>
/// <param name="turnContext">A Slack-specific turn context for the current turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="feedbackData">The feedback loop data.</param>
/// <param name="cancellationToken">A cancellation token that can be used to observe cancellation.</param>
/// <returns>A task that represents the asynchronous handler operation.</returns>
public delegate Task SlackFeedbackLoopHandler(ISlackTurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken);
