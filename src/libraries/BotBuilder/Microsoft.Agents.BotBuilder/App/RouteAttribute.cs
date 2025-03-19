// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Errors;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.BotBuilder.App
{
    public enum RouteType
    {
        Activity,
        Message,
        Conversation,
        HandOff,
        ReactionAdded,
        ReactionRemoved
    }

    /// <summary>
    /// Adds an AgentApplication Routes
    /// 
    /// Route Type:
    /// <code>
    ///    Activity,       // { ActivityType | RegEx | Selector}, Rank
    ///    Message,        // { ActivityText | RegEx | Selector}, Rank
    ///    Conversation,   // { Event | Selector}, Rank
    ///    HandOff,        // Selector, Rank
    ///    ReactionAdded,  // Rank
    ///    ReactionRemoved // Rank
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class RouteAttribute : Attribute, IRouteAttribute
    {
        public RouteType Type { get; set; }

        public string ActivityType { get; set; }
        public string ActivityText { get; set; }

        public string Regex { get; set; }

        public string Selector { get; set; }

        public string Event { get; set; }


        public ushort Rank { get; set; } = RouteRank.Unspecified;

        public void AddRoute(AgentApplication app, MethodInfo attributedMethod)
        {
            if (Type == RouteType.Activity)
            {
                if (!string.IsNullOrWhiteSpace(ActivityType))
                {
                    app.OnActivity(ActivityType, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
                    app.OnActivity(new Regex(Regex), attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateSelectorDelegate<RouteSelector>(app, Selector, selectorMethod, out var delegateSelector);
                    CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);

                    app.OnActivity(delegateSelector, delegateHandler, rank: Rank);
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeMissingArgs, null);
                }
            }
            else if (Type == RouteType.Message)
            {
                if (!string.IsNullOrWhiteSpace(ActivityText))
                {
                    app.OnMessage(ActivityText, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
                    app.OnMessage(new Regex(Regex), attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateSelectorDelegate<RouteSelector>(app, Selector, selectorMethod, out var delegateSelector);
                    CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);

                    app.OnMessage(delegateSelector, delegateHandler, rank: Rank);
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeMissingArgs, null);
                }
            }
            else if (Type == RouteType.Conversation)
            {
                if (!string.IsNullOrWhiteSpace(Event))
                {
                    app.OnConversationUpdate(Event, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateSelectorDelegate<RouteSelector>(app, Selector, selectorMethod, out var delegateSelector);
                    CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);

                    app.OnConversationUpdate(delegateSelector, delegateHandler, rank: Rank);
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeMissingArgs, null);
                }
            }
            else if (Type == RouteType.ReactionAdded)
            {
                CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);
                app.OnMessageReactionsAdded(delegateHandler, rank: Rank);
            }
            else if (Type == RouteType.ReactionRemoved)
            {
                CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);
                app.OnMessageReactionsRemoved(delegateHandler, rank: Rank);
            }
            else if (Type == RouteType.HandOff)
            {
                CreateHandlerDelegate<HandoffHandler>(app, attributedMethod, out var delegateHandler);
                app.OnHandoff(delegateHandler, rank: Rank);
            }
        }

        private static void GetSelectorMethodInfo(AgentApplication app, string selectorName, out MethodInfo selectorMethod)
        {
            selectorMethod = app.GetType().GetMethod(selectorName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorNotFound, null, selectorName);
        }

        private static void CreateSelectorDelegate<T>(AgentApplication app, string selectorName, MethodInfo selectorMethod, out T delegateSelector) 
            where T : class, Delegate
        {
            try
            {
                delegateSelector = selectorMethod.CreateDelegate<T>(app);
            }
            catch (ArgumentException ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorInvalid, ex, selectorName);
            }
        }

        private static void CreateHandlerDelegate<T>(AgentApplication app, MethodInfo attributedMethod, out T delegateHandler)
            where T : class, Delegate
        {
            try
            {
                delegateHandler = attributedMethod.CreateDelegate<T>(app);
            }
            catch (ArgumentException ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeHandlerInvalid, ex);
            }
        }
    }
}
