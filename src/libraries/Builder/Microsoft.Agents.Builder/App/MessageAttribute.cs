// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models.Activities;
using System;
using System.Reflection;

namespace Microsoft.Agents.Builder.App
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageAttribute : Attribute, IRouteAttribute
    {
        public string Text { get; set; }

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

            if (string.IsNullOrWhiteSpace(Text))
            {
#if !NETSTANDARD
                app.OnActivity<IMessageActivity>(attributedMethod.CreateDelegate<RouteHandler<IMessageActivity>>(app), isAgenticOnly: IsAgentic, rank: Rank, autoSignInHandlers: autoSignInHandlers);
#else
                app.OnActivity<IMessageActivity>((RouteHandler<IMessageActivity>)attributedMethod.CreateDelegate(typeof(RouteHandler<IMessageActivity>), app), isAgenticOnly: IsAgentic, rank: Rank, autoSignInHandlers: autoSignInHandlers);
#endif
            }
            else
            {
#if !NETSTANDARD
                app.OnMessage(Text, attributedMethod.CreateDelegate<RouteHandler<IMessageActivity>>(app), isAgenticOnly: IsAgentic, rank: Rank, autoSignInHandlers: autoSignInHandlers);
#else
                app.OnMessage(Text, (RouteHandler<IMessageActivity>)attributedMethod.CreateDelegate(typeof(RouteHandler<IMessageActivity>), app), isAgenticOnly: IsAgentic, rank: Rank, autoSignInHandlers: autoSignInHandlers);
#endif
            }
        }
    }
}
