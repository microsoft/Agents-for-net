﻿using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    /// <summary>
    /// Handler to perform the action.
    /// </summary>
    /// <typeparam name="TState">Type of the turn state.</typeparam>
    public interface IActionHandler<TState> where TState : ITurnState
    {
        /// <summary>
        /// Perform the action.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="turnState">Current turn state.</param>
        /// <param name="entities">Optional entities to be used to perform the action.</param>
        /// <param name="action">The actual action name.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The result of the action handler.</returns>
        Task<string> PerformActionAsync(ITurnContext turnContext, TState turnState, object? entities = null, string? action = null, CancellationToken cancellationToken = default);
    }
}
