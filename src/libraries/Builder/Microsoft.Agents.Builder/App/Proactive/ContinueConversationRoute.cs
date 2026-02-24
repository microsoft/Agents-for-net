// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using System;
using System.Reflection;

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
        /// <param name="delegatgeName">The name of the delegate to be used for continuing the conversation. Cannot be null or empty.</param>
        /// <param name="delimitedTokenHandlers">A comma-separated string containing the names of token handlers to associate with the route. If null or
        /// empty, no token handlers are assigned.</param>
        public ContinueConversationRoute(string delegatgeName, string delimitedTokenHandlers = null) : this(delegatgeName, DelimitedToList(delimitedTokenHandlers))
        {
        }

        /// <summary>
        /// Initializes a new instance of the ContinueConversationRoute class with the specified delegate method
        /// name and optional token handlers.
        /// </summary>
        /// <param name="delegatgeName">The name of the method on the agent type to be used as the continue route handler. This method must
        /// exist on the agent and can be public or non-public.</param>
        /// <param name="tokenHandlers">An optional array of token handler names to associate with this route. May be null if no token handlers
        /// are required.</param>
        /// <exception cref="InvalidOperationException">Thrown if a method with the specified name does not exist on the agent type.</exception>
        public ContinueConversationRoute(string delegatgeName, string[] tokenHandlers = null)
        {
            _delegate = typeof(TAgent).GetMethod(delegatgeName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException($"The specified continue route handler '{delegatgeName}' was not found on AgentApplication '{typeof(TAgent).FullName}'.");
            TokenHandlers = tokenHandlers;
        }

        /// <summary>
        /// Gets the delegate associated with this instance.
        /// </summary>
        public RouteHandler RouteHandler(TAgent agent) => CreateHandlerDelegate<RouteHandler>(agent, _delegate);

        /// <summary>
        /// Gets the collection of token handler names used to process authentication tokens.
        /// </summary>
        public string[] TokenHandlers { get; private set; }

        private static string[] DelimitedToList(string delimitedTokenHandlers)
        {
#if !NETSTANDARD
            return !string.IsNullOrEmpty(delimitedTokenHandlers) ? delimitedTokenHandlers.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null;
#else
            return !string.IsNullOrEmpty(delimitedTokenHandlers) ? delimitedTokenHandlers.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries) : null;
#endif
        }

        private static T CreateHandlerDelegate<T>(TAgent agent, MethodInfo attributedMethod)
    where T : class, Delegate
        {
            try
            {
#if !NETSTANDARD
                return attributedMethod.CreateDelegate<T>(agent);
#else
                return (T)attributedMethod.CreateDelegate(typeof(T), agent);
#endif
            }
            catch (ArgumentException ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeHandlerInvalid, ex);
            }
        }
    }
}