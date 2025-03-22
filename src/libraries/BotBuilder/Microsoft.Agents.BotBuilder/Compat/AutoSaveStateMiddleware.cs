// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.BotBuilder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Compat
{
    /// <summary>
    ///  Middleware to automatically persist state before the end of each turn.
    /// </summary>
    /// <remarks>
    /// This calls <see cref="IBotState.SaveChangesAsync(ITurnContext, bool, CancellationToken)"/>
    /// on each state object it manages.
    /// </remarks>
    public class AutoSaveStateMiddleware : IMiddleware
    {
        private readonly bool _autoLoad;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoSaveStateMiddleware"/> class.
        /// </summary>
        /// <param name="botStates">initial list of <see cref="BotState"/> objects to manage.</param>
        public AutoSaveStateMiddleware(params IBotState[] botStates)
        {
            // This is really so back-compat bots can use the new BotState methods without having to
            // Load or use IStatePropertyAccessor.
            _autoLoad = true;  
            TurnState = new TurnState(botStates);
        }

        /// <summary>
        /// Allows for optionally auto-loading BotState at turn start.
        /// </summary>
        /// <param name="autoLoad"></param>
        /// <param name="botStates"></param>
        public AutoSaveStateMiddleware(bool autoLoad, params IBotState[] botStates)
        {
            _autoLoad = autoLoad;
            TurnState = new TurnState(botStates);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoSaveStateMiddleware"/> class with 
        /// a list of state management objects managed by this object.
        /// </summary>
        /// <param name="botStateSet">The state management objects managed by this object.</param>
        public AutoSaveStateMiddleware(ITurnState botStateSet)
        {
            TurnState = botStateSet;
        }

        /// <summary>
        /// Gets or sets the list of state management objects managed by this object.
        /// </summary>
        /// <value>The state management objects managed by this object.</value>
        public ITurnState TurnState { get; set; }

        /// <summary>
        /// Before the turn ends, calls <see cref="BotState.SaveChangesAsync(ITurnContext, bool, CancellationToken)"/>
        /// on each state object.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="next">The delegate to call to continue the bot middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>This middleware persists state after the bot logic completes and before the turn ends.</remarks>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default(CancellationToken))
        {
            // before turn
            if (_autoLoad)
            {
                await TurnState.LoadStateAsync(turnContext, cancellationToken:cancellationToken, force:true).ConfigureAwait(false);
            }

            await next(cancellationToken).ConfigureAwait(false);

            // after turn
            await TurnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
