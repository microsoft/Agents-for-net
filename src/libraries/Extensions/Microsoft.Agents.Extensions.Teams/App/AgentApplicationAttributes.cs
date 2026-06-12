// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.App;

/// <summary>
/// Attribute to define a Teams-specific message route for an <see cref="AgentApplication"/> method.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for Teams message activities.
/// Provide <paramref name="text"/> for an exact match, <paramref name="textRegex"/> for a pattern match, or neither
/// to match any Teams message activity.
/// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
/// </remarks>
/// <param name="text">The exact Teams message text to match. Mutually exclusive with <paramref name="textRegex"/>.</param>
/// <param name="textRegex">A regular expression pattern matched against the incoming message text. Mutually exclusive with <paramref name="text"/>.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
/// <param name="rank">Route evaluation order. Lower values run first.</param>
/// <param name="autoSignInHandlers">A comma, space, or semicolon-delimited list of OAuth sign-in handler names, or the name of a method that resolves them.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamsMessageRouteAttribute(string text = null, string textRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
{
    /// <summary>
    /// Adds the route described by this attribute to the supplied <see cref="AgentApplication"/>.
    /// </summary>
    /// <param name="app">The agent application receiving the route.</param>
    /// <param name="method">The attributed method to convert into a Teams route handler.</param>
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
        var b = TeamsMessageRouteBuilder.Create().WithHandler(handler, app.Proactive).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));

        if (!string.IsNullOrWhiteSpace(text))
        {
            b = b.WithText(text);
        }
        else if (!string.IsNullOrWhiteSpace(textRegex))
        {
            b = b.WithText(new Regex(textRegex));
        }

        app.AddRoute(b.Build());
    }
}