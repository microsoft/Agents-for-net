// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Provides a base wrapper around an <see cref="ITurnContext"/> instance.
    /// </summary>
    /// <remarks>
    /// This class delegates all <see cref="ITurnContext"/> members to an inner turn context,
    /// allowing derived types to extend turn-context behavior without reimplementing the
    /// underlying operations.
    /// </remarks>
    public abstract class TurnContextWrapper : ITurnContext
    {

        protected readonly ITurnContext _turnContext;

        /// <summary>
        /// Gets the Adapter that created this context object.
        /// </summary>
        /// <value>The Adapter that created this context object.</value>
        public IChannelAdapter Adapter
        {
            get { return _turnContext.Adapter; }
        }

        /// <summary>
        /// Gets the services registered on this context object.
        /// </summary>
        /// <value>The services registered on this context object.</value>
        public TurnContextStateCollection Services
        {
            get { return _turnContext.Services; }
        }

        /// <summary>
        /// Gets the state collection for the turn context.
        /// </summary>
        public TurnContextStateCollection StackState
        {
            get { return _turnContext.StackState; }
        }

        /// <summary>
        /// Gets the activity associated with this turn; or <c>null</c> when processing
        /// a proactive message.
        /// </summary>
        /// <value>The activity associated with this turn.</value>
        public IActivity Activity
        {
            get { return _turnContext.Activity; }
        }

        /// <inheritdoc/>
        public IStreamingResponse StreamingResponse
        {
            get { return _turnContext.StreamingResponse; }
        }

        /// <summary>
        /// Gets a value indicating whether at least one response was sent for the current turn.
        /// </summary>
        /// <value><c>true</c> if at least one response was sent for the current turn.</value>
        public bool Responded
        {
            get { return _turnContext.Responded; }
        }

        /// <summary>
        /// Gets the claims identity associated with this turn context.
        /// </summary>
        public ClaimsIdentity Identity
        {
            get { return _turnContext.Identity; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TurnContextWrapper"/> class.
        /// </summary>
        /// <param name="turnContext">The inner turn context to wrap.</param>
        protected TurnContextWrapper(ITurnContext turnContext)
        {
            this._turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
        }

        /// <inheritdoc/>
        public virtual Task<ResourceResponse> SendActivityAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(text, speak, inputHint, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(activity, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivitiesAsync(activities, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.UpdateActivityAsync(activity, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(activityId, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(conversationReference, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            _turnContext.OnSendActivities(handler);
            return this;
        }

        /// <inheritdoc/>
        public virtual ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            _turnContext.OnUpdateActivity(handler);
            return this;
        }

        /// <inheritdoc/>
        public virtual ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            _turnContext.OnDeleteActivity(handler);
            return this;
        }

        /// <inheritdoc/>
        public virtual Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
        {
            return _turnContext.TraceActivityAsync(name, value, valueType, label, cancellationToken);
        }
    }
}