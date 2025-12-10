// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models.Activities;
using System;
using System.Reflection;

namespace Microsoft.Agents.Builder.App
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ConversationUpdateAttribute : Attribute, IRouteAttribute
    {
        public string Event { get; set; }

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
        public string SignInHandlers { get; set; }

        public void AddRoute(AgentApplication app, MethodInfo attributedMethod)
        {
#if !NETSTANDARD
            string[] autoSignInHandlers = !string.IsNullOrEmpty(SignInHandlers) ? SignInHandlers.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null;
#else
            string[] autoSignInHandlers = !string.IsNullOrEmpty(SignInHandlers) ? SignInHandlers.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries) : null;
#endif

            if (!string.IsNullOrWhiteSpace(Event))
            {
#if !NETSTANDARD
                app.OnConversationUpdate(Event, attributedMethod.CreateDelegate<RouteHandler<IConversationUpdateActivity>>(app), isAgenticOnly: IsAgentic, rank: Rank, autoSignInHandlers: autoSignInHandlers);
#else
                app.OnConversationUpdate(Event, (RouteHandler<IConversationUpdateActivity>)attributedMethod.CreateDelegate(typeof(RouteHandler<IConversationUpdateActivity>), app), isAgenticOnly: IsAgentic, rank: Rank, autoSignInHandlers: autoSignInHandlers);
#endif
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeMissingArgs, null);
            }
        }
    }
}
