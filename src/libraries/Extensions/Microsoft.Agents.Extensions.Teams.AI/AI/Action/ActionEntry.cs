﻿using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.State;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    /// <summary>
    /// Represents an action.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    internal sealed class ActionEntry<TState> where TState : ITurnState
    {
        /// <summary>
        /// The action name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The action handler function.
        /// </summary>
        public IActionHandler<TState> Handler { get; set; }

        /// <summary>
        /// Whether to allow overrides of this action's properties.
        /// </summary>
        public bool AllowOverrides { get; set; }


        /// <summary>
        /// Creates a new instance of the <see cref="ActionEntry{TState}"/> class.
        /// </summary>
        /// <param name="name">The action name.</param>
        /// <param name="handler">The action handler function.</param>
        /// <param name="allowOverrides">Whether to allow overrides of this action's properties.</param>
        public ActionEntry(string name, IActionHandler<TState> handler, bool allowOverrides = true)
        {
            Name = name;
            Handler = handler;
            AllowOverrides = allowOverrides;
        }

        /// <summary>
        /// Converts a method with the <see cref="ActionAttribute"/> to an <see cref="ActionEntry{TState}"/>.
        /// </summary>
        /// <param name="methodSignature">The method signature.</param>
        /// <param name="methodContainerInstance">The instance of a class which contains the method.</param>
        /// <returns>
        /// The <see cref="ActionEntry{TState}"/> if the given method adheres to the <see cref="ActionHandler{TState}"/> delegate
        /// and has the <see cref="ActionAttribute"/>. Otherwise, returns null.
        /// </returns>
        /// <exception cref="System.Exception"></exception>
        public static ActionEntry<TState>? FromNativeMethod(MethodInfo methodSignature, object methodContainerInstance)
        {
            if (methodSignature == null)
            {
                throw new ArgumentNullException(nameof(methodSignature), "Method is null");
            }

            if (methodContainerInstance == null)
            {
                throw new ArgumentNullException(nameof(methodContainerInstance), "Method container instance is null");
            }

            ActionAttribute? actionAttribute = methodSignature
                .GetCustomAttributes(typeof(ActionAttribute), true)
                .Cast<ActionAttribute>()
                .FirstOrDefault();

            if (actionAttribute == null)
            {
                return null;
            }

            string name = actionAttribute.Name;
            IActionHandler<TState> handler = new ActionHandler<TState>(methodSignature, methodContainerInstance);
            bool allowOverrides = actionAttribute.IsDefault;

            return new ActionEntry<TState>(name, handler, allowOverrides);
        }
    }
}
