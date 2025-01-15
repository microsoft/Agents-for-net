// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder
{
    /// <summary>
    /// Represents a bot that can operate on incoming activities.
    /// </summary>
    /// <remarks>
    /// A <see cref="IChannelAdapter"/> passes incoming activities from the channel
    /// to the bot's <see cref="OnTurnAsync(ITurnContext, CancellationToken)"/> method.
    /// </remarks>
    /// <seealso cref="BotCallbackHandler"/>
    public interface IBot
    {
        /// <summary>
        /// When implemented in a bot, handles an incoming activity.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>The <paramref name="turnContext"/> provides information about the
        /// incoming activity, and other data needed to process the activity.</remarks>
        /// <seealso cref="ITurnContext"/>
        Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
    }
}
