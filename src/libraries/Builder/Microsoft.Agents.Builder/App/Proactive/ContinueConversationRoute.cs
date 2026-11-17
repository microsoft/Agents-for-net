// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Represents routing information for continuing a conversation, including the delegate name and associated
    /// token handlers.
    /// </summary>
    public class ContinueConversationRoute<TAgent> where TAgent : AgentApplication
    {
        private readonly MethodInfo _delegate;

        /// <summary>
        /// Initializes a new instance of the ContinueConversationRoute class using the specified delegate name and a
        /// comma-delimited list of token handler names.
        /// </summary>
        /// <remarks>If multiple token handler names are provided in the delimitedTokenHandlers parameter,
        /// they are split and trimmed before being assigned. This constructor is useful when token handler names are
        /// provided as a single string rather than an array.</remarks>
        /// <param name="delegateName">The name of the delegate to be used for continuing the conversation. Cannot be null or empty.</param>
        /// <param name="delimitedTokenHandlers">A comma-separated string containing the names of token handlers to associate with the route. If null or
        /// empty, no token handlers are assigned.</param>
        public ContinueConversationRoute(string delegateName, string delimitedTokenHandlers = null) : this(delegateName, RouteAttributeHelper.DelimitedToList(delimitedTokenHandlers))
        {
        }

        /// <summary>
        /// Initializes a new instance of the ContinueConversationRoute class with the specified delegate method
        /// name and optional token handlers.
        /// </summary>
        /// <param name="delegateName">The name of the method on the agent type to be used as the continue route handler. This method must
        /// exist on the agent and can be public or non-public.</param>
        /// <param name="tokenHandlers">An optional array of token handler names to associate with this route. May be null if no token handlers
        /// are required.</param>
        /// <exception cref="InvalidOperationException">Thrown if a method with the specified name does not exist on the agent type.</exception>
        public ContinueConversationRoute(string delegateName, string[] tokenHandlers = null)
        {
            _delegate = typeof(TAgent).GetMethod(delegateName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException($"The specified continue route handler '{delegateName}' was not found on AgentApplication '{typeof(TAgent).FullName}'.");
            TokenHandlers = tokenHandlers;
        }

        /// <summary>
        /// Gets the delegate associated with this instance.
        /// </summary>
        public RouteHandler RouteHandler(TAgent agent)
        {
            try
            {
                var parameters = _delegate.GetParameters();
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType != typeof(ITurnContext) &&
                    typeof(ITurnContext).IsAssignableFrom(parameters[0].ParameterType))
                {
                    return (RouteHandler)typeof(ContinueConversationRoute<TAgent>)
                        .GetMethod(nameof(CreateTypedHandler), BindingFlags.Instance | BindingFlags.NonPublic)
                        .MakeGenericMethod(parameters[0].ParameterType)
                        .Invoke(this, [agent]);
                }

                return RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(agent, _delegate);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeHandlerInvalid, ex.InnerException);
            }
            catch (ArgumentException ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeHandlerInvalid, ex);
            }
        }

        /// <summary>
        /// Gets the collection of token handler names used to process authentication tokens.
        /// </summary>
        public string[] TokenHandlers { get; private set; }

        public override string ToString() => $"ContinueConversationRoute: Delegate={_delegate.Name}, TokenHandlers=[{string.Join(", ", TokenHandlers ?? [])}]";

        private RouteHandler CreateTypedHandler<TContext>(TAgent agent) where TContext : ITurnContext
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<Func<TContext, ITurnState, CancellationToken, Task>>(agent, _delegate);
            var continuation = new ContinueConversationHandler(_delegate,
                (context, state, ct) => handler(GetTurnContext<TContext>(agent, context), state, ct));
            return continuation.InvokeAsync;
        }

        private TContext GetTurnContext<TContext>(TAgent agent, ITurnContext context) where TContext : ITurnContext
        {
            if (context is TContext typedContext)
            {
                return typedContext;
            }

            foreach (var registered in agent.RegisteredExtensions)
            {
                if (registered is AgentExtension extension &&
                    extension.ChannelId != null &&
                    (extension.ChannelId == context.Activity.ChannelId ||
                     (!extension.ChannelId.IsSubChannel() && extension.ChannelId.IsParentChannel(context.Activity.ChannelId?.Channel))) &&
                    extension.WrapTurnContext(context) is TContext wrappedContext)
                {
                    return wrappedContext;
                }
            }

            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.ProactiveTurnContextUnavailable, null, _delegate.Name, typeof(TContext).FullName, context.Activity.ChannelId);
        }
    }

    internal sealed class ContinueConversationHandler(MethodInfo method, RouteHandler handler)
    {
        internal MethodInfo Method { get; } = method;

        internal Task InvokeAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken)
            => handler(context, state, cancellationToken);
    }
}
