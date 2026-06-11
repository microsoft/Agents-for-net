// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams;

[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamsMessageRouteAttribute(string text = null, string textRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
{
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