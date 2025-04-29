﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Builder.App
{
    public enum RouteType
    {
        Activity,
        Message,
        Event,
        Conversation,
        HandOff,
        ReactionAdded,
        ReactionRemoved
    }

    /// <summary>
    /// Adds an AgentApplication Routes
    /// 
    /// RouteType:
    /// <code>
    ///    Activity,       // { Type | RegEx | Selector}, Rank
    ///    Message,        // { Text | RegEx | Selector}, Rank
    ///    Event,          // { EventName | RegEx | Selector}, Rank
    ///    Conversation,   // { EventName | Selector}, Rank
    ///    HandOff,        // Selector, Rank
    ///    ReactionAdded,  // Rank
    ///    ReactionRemoved // Rank
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class RouteAttribute : Attribute, IRouteAttribute
    {
        public RouteType RouteType { get; set; }

        public string Type { get; set; }
        public string Text { get; set; }

        public string Regex { get; set; }

        public string Selector { get; set; }

        public string EventName { get; set; }


        public ushort Rank { get; set; } = RouteRank.Unspecified;

        public void AddRoute(AgentApplication app, MethodInfo attributedMethod)
        {
            if (RouteType == RouteType.Activity)
            {
                if (!string.IsNullOrWhiteSpace(Type))
                {
#if !NETSTANDARD
                    app.OnActivity(Type, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnActivity(Type, (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler),app), rank: Rank);
#endif
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
#if !NETSTANDARD
                    app.OnActivity(new Regex(Regex), attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnActivity(new Regex(Regex), (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler), app), rank: Rank);
#endif
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
            else if (RouteType == RouteType.Message)
            {
                if (!string.IsNullOrWhiteSpace(Text))
                {
#if !NETSTANDARD
                    app.OnMessage(Text, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnMessage(Text, (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler), app), rank: Rank);
#endif
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
#if !NETSTANDARD
                    app.OnMessage(new Regex(Regex), attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnMessage(new Regex(Regex), (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler), app), rank: Rank);
#endif
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
            else if (RouteType == RouteType.Event)
            {
                if (!string.IsNullOrWhiteSpace(EventName))
                {
#if !NETSTANDARD
                    app.OnEvent(EventName, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnEvent(EventName, (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler), app), rank: Rank);
#endif
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
#if !NETSTANDARD
                    app.OnEvent(new Regex(Regex), attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnEvent(new Regex(Regex), (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler), app), rank: Rank);
#endif
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateSelectorDelegate<RouteSelector>(app, Selector, selectorMethod, out var delegateSelector);
                    CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);

                    app.OnEvent(delegateSelector, delegateHandler, rank: Rank);
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeMissingArgs, null);
                }
            }
            else if (RouteType == RouteType.Conversation)
            {
                if (!string.IsNullOrWhiteSpace(EventName))
                {
#if !NETSTANDARD
                    app.OnConversationUpdate(EventName, attributedMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
#else
                    app.OnConversationUpdate(EventName, (RouteHandler)attributedMethod.CreateDelegate(typeof(RouteHandler), app), rank: Rank);
#endif
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
            else if (RouteType == RouteType.ReactionAdded)
            {
                CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);
                app.OnMessageReactionsAdded(delegateHandler, rank: Rank);
            }
            else if (RouteType == RouteType.ReactionRemoved)
            {
                CreateHandlerDelegate<RouteHandler>(app, attributedMethod, out var delegateHandler);
                app.OnMessageReactionsRemoved(delegateHandler, rank: Rank);
            }
            else if (RouteType == RouteType.HandOff)
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
#if !NETSTANDARD
                delegateSelector = selectorMethod.CreateDelegate<T>(app);
#else
                delegateSelector = (T)selectorMethod.CreateDelegate(typeof(T), app);
#endif
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
#if !NETSTANDARD
                delegateHandler = attributedMethod.CreateDelegate<T>(app);
#else
                delegateHandler = (T)attributedMethod.CreateDelegate(typeof(T), app);
#endif
            }
            catch (ArgumentException ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeHandlerInvalid, ex);
            }
        }
    }
}
