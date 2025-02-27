// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Errors;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.BotBuilder.App
{
    /// <summary>
    /// Adds an AgentApplication.OnActivity route.
    /// Only one of Type, Regex, or Selector will be used:
    /// 1. Type
    /// 2. Regex
    /// 3. Selector
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ActivityRouteAttribute : Attribute, IRouteAttribute
    {
        public string Type { get; set; }
             
        public string Regex { get; set; }

        public string Selector { get; set; }

        public ushort Rank { get; set; } = RouteRank.Unspecified;

        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            if (!string.IsNullOrWhiteSpace(Type))
            {
                app.OnActivity(Type, method.CreateDelegate<RouteHandler>(app), rank: Rank );
            }
            else if (!string.IsNullOrWhiteSpace(Regex))
            {
                app.OnActivity(new Regex(Regex), method.CreateDelegate<RouteHandler>(app), rank: Rank);
            }
            else if (!string.IsNullOrWhiteSpace(Selector))
            {
                var selectorMethod = app.GetType().GetMethod(Selector, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) 
                    ?? throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorNotFound, null);

                try
                {
                    var delegateSelector = selectorMethod.CreateDelegate<RouteSelectorAsync>(app);
                    var delegateHandler = method.CreateDelegate<RouteHandler>(app);
                    app.OnActivity(delegateSelector, delegateHandler, rank: Rank);
                }
                catch (ArgumentException ex)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorInvalid, ex);
                }
            }
        }
    }
}
