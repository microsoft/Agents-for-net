// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.BotBuilder
{
    /// <inheritdoc/>
    /// <summary>
    /// Initializes a new instance of the <see cref="IChannelAdapter"/> class.
    /// </summary>
    public abstract class ChannelAdapter(ILogger logger = null) : IChannelAdapter
    {
        /// <summary>
        /// Logger for the bot adapter. 
        /// </summary>
        private readonly ILogger? _logger = logger ?? NullLogger.Instance;

        /// <inheritdoc/>
        public Func<ITurnContext, Exception, Task> OnTurnError { get; set; }

        /// <summary>
        /// Gets the collection of middleware in the adapter's pipeline.
        /// </summary>
        /// <value>The middleware collection for the pipeline.</value>
        public IMiddlewareSet MiddlewareSet { get; } = new MiddlewareSet();

        /// <summary>
        /// Adds middleware to the adapter's pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The updated adapter object.</returns>
        /// <remarks>Middleware is added to the adapter at initialization time.
        /// For each turn, the adapter calls middleware in the order in which you added it.
        /// </remarks>
        public IChannelAdapter Use(IMiddleware middleware)
        {
            MiddlewareSet.Use(middleware);
            return this;
        }

        /// <inheritdoc/>
        public abstract Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public virtual Task CreateConversationAsync(string botAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual Task ContinueConversationAsync(string botId, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            using (var context = new TurnContext(this, reference.GetContinuationActivity()))
            {
                return RunPipelineAsync(context, callback, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public virtual Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual Task ContinueConversationAsync(string botId, IActivity continuationActivity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public virtual Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public virtual Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Starts activity processing for the current bot turn.
        /// </summary>
        /// <param name="turnContext">The turn's context object.</param>
        /// <param name="callback">A callback method to run at the end of the pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="turnContext"/> is null.</exception>
        /// <remarks>The adapter calls middleware in the order in which you added it.
        /// The adapter passes in the context object for the turn and a next delegate,
        /// and the middleware calls the delegate to pass control to the next middleware
        /// in the pipeline. Once control reaches the end of the pipeline, the adapter calls
        /// the <paramref name="callback"/> method. If a middleware component doesn't call
        /// the next delegate, the adapter does not call  any of the subsequent middleware’s
        /// <see cref="IMiddleware.OnTurnAsync(ITurnContext, NextDelegate, CancellationToken)"/>
        /// methods or the callback method, and the pipeline short circuits.
        /// <para>When the turn is initiated by a user activity (reactive messaging), the
        /// callback method will be a reference to the bot's
        /// <see cref="IBot.OnTurnAsync(ITurnContext, CancellationToken)"/> method. When the turn is
        /// initiated by a call to <see cref="ContinueConversationAsync(string, ConversationReference, BotCallbackHandler, CancellationToken)"/>
        /// (proactive messaging), the callback method is the callback method that was provided in the call.</para>
        /// </remarks>
        protected async Task RunPipelineAsync(ITurnContext turnContext, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            // Call any registered Middleware Components looking for ReceiveActivityAsync()
            if (turnContext.Activity != null)
            {
                try
                {
                    await MiddlewareSet.ReceiveActivityWithStatusAsync(turnContext, callback, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (OnTurnError != null)
                    {
                        _logger?.LogError(exception: e, $"Handled Exception in {this.GetType().Name}");
                        await OnTurnError.Invoke(turnContext, e).ConfigureAwait(false);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                // call back to caller on proactive case
                if (callback != null)
                {
                    await callback(turnContext, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
