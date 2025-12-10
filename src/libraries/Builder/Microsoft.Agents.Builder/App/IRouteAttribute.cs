// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Agents.Builder.App
{
    internal interface IRouteAttribute
    {
        /// <summary>
        /// Indicates if this is an Agentic route.  Defaults to false.
        /// </summary>
        bool IsAgentic { get; set; }

        /// <summary>
        /// Route ordering rank.
        /// </summary>
        /// <remarks>
        /// 0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.
        /// </remarks>
        ushort Rank { get; set; }

        /// <summary>
        /// Delimited list of OAuth handlers to use for the RouteHandler.
        /// </summary>
        /// <remarks>
        /// Valid delimiters are: comma, space, or semi-colon.
        /// </remarks>
        string SignInHandlers { get; set; }

        void AddRoute(AgentApplication app, MethodInfo method);
    }
}
