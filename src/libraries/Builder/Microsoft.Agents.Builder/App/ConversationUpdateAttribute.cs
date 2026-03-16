// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Reflection;

namespace Microsoft.Agents.Builder.App
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ConversationUpdateAttribute : Attribute, IRouteAttribute
    {
        public ConversationUpdateAttribute(string eventName) 
        { 
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeMissingArgs, null, nameof(eventName));
            }
            Event = eventName;
        }

        public string Event { get; }

        /// <summary>
        /// Indicates if this is an Agentic route.  Defaults to false.
        /// </summary>
        public bool IsAgentic { get; set; }

        /// <summary>
        /// Route ordering rank.
        /// </summary>
        /// <remarks>
        /// 0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.
        /// </remarks>
        public ushort Rank { get; set; } = RouteRank.Unspecified;

        /// <summary>
        /// Delimited list of OAuth handlers to use for the RouteHandler.
        /// </summary>
        /// <remarks>
        /// Valid delimiters are: comma, space, or semi-colon.
        /// </remarks>
        public string AutoSignInHandlers { get; set; }

        public void AddRoute(AgentApplication app, MethodInfo attributedMethod)
        {
#if !NETSTANDARD
            string[] autoSignInHandlers = !string.IsNullOrEmpty(AutoSignInHandlers) ? AutoSignInHandlers.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null;
#else
            string[] autoSignInHandlers = !string.IsNullOrEmpty(AutoSignInHandlers) ? AutoSignInHandlers.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries) : null;
#endif

#if !NETSTANDARD
            var handler = attributedMethod.CreateDelegate<RouteHandler<IConversationUpdateActivity>>(app);
#else
            var handler = (RouteHandler<IConversationUpdateActivity>)attributedMethod.CreateDelegate(typeof(RouteHandler<IConversationUpdateActivity>), app);
#endif

            app.AddRoute(
                ConversationUpdateRouteBuilder.Create()
                    .WithUpdateEvent(Event)
                    .WithHandler(handler)
                    .AsAgentic(IsAgentic)
                    .WithOrderRank(Rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .Build()
            );
        }
    }
}
