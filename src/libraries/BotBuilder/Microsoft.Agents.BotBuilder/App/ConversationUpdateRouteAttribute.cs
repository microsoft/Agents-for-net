// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reflection;

namespace Microsoft.Agents.BotBuilder.App
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ConversationUpdateRouteAttribute : Attribute, IRouteAttribute
    {
        public string Event { get; set; }
             
        public ushort Rank { get; set; } = RouteRank.Unspecified;

        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            if (!string.IsNullOrWhiteSpace(Event))
            {
                app.OnConversationUpdate(Event, method.CreateDelegate<RouteHandler>(app), Rank);
            }
        }
    }
}
