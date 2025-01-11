// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder
{
    /// <summary>
    /// A TurnContext with a strongly typed Activity property that wraps an untyped inner TurnContext.
    /// </summary>
    /// <typeparam name="T">An IActivity derived type, that is one of IMessageActivity, IConversationUpdateActivity etc.</typeparam>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TypedTurnContext{T}"/> class.
    /// </remarks>
    /// <param name="innerTurnContext">The inner turn context.</param>
    internal class TypedTurnContext<T>(ITurnContext innerTurnContext) : ITurnContext<T>
        where T : IActivity
    {
        private readonly ITurnContext _innerTurnContext = innerTurnContext;

        /// <summary>
        /// Gets the inner  context's activity, cast to the type parameter of this <see cref="TypedTurnContext{T}"/>.
        /// </summary>
        /// <value>The inner context's activity.</value>
        T ITurnContext<T>.Activity => (T)_innerTurnContext.Activity;

        public IConnectorClient Connector => TurnState.Get<IConnectorClient>();

        /// <summary>
        /// Gets the bot adapter that created this context object.
        /// </summary>
        /// <value>The bot adapter that created this context object.</value>
        public IChannelAdapter Adapter => _innerTurnContext.Adapter;

        /// <summary>
        /// Gets the collection of values cached with the context object for the lifetime of the turn.
        /// </summary>
        /// <value>The collection of services registered on this context object.</value>
        public TurnContextStateCollection TurnState => _innerTurnContext.TurnState;

        /// <summary>
        /// Gets the activity for this turn of the bot.
        /// </summary>
        /// <value>The activity for this turn of the bot.</value>
        public IActivity Activity => _innerTurnContext.Activity;

        /// <summary>
        /// Gets a value indicating whether at least one response was sent for the current turn.
        /// </summary>
        /// <value><c>true</c> if at least one response was sent for the current turn; otherwise, <c>false</c>.</value>
        /// <seealso cref="SendActivityAsync(IActivity, CancellationToken)"/>
        public bool Responded => _innerTurnContext.Responded;

        IActivity ITurnContext.Activity => _innerTurnContext.Activity;

        /// <inheritdoc/>
        public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.DeleteActivityAsync(activityId, cancellationToken);

        /// <inheritdoc/>
        public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.DeleteActivityAsync(conversationReference, cancellationToken);

        /// <inheritdoc/>
        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
            => _innerTurnContext.OnDeleteActivity(handler);

        /// <inheritdoc/>
        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
            => _innerTurnContext.OnSendActivities(handler);

        /// <inheritdoc/>
        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
            => _innerTurnContext.OnUpdateActivity(handler);

        /// <inheritdoc/>
        public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.SendActivitiesAsync(activities, cancellationToken);

        /// <inheritdoc/>
        public Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string speak = null, string inputHint = InputHints.AcceptingInput, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.SendActivityAsync(textReplyToSend, speak, inputHint, cancellationToken);

        /// <inheritdoc/>
        public Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.SendActivityAsync(activity, cancellationToken);

        /// <inheritdoc/>
        public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.UpdateActivityAsync(activity, cancellationToken);

        /// <inheritdoc/>
        public Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default(CancellationToken))
            => _innerTurnContext.TraceActivityAsync(name, value, valueType, label, cancellationToken);

    }
}
