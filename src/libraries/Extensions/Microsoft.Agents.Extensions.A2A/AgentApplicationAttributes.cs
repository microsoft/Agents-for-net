// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Attribute to define a route that handles A2A message activities, optionally matching specific text or a text pattern.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message activities received on the A2A channel.
/// This differs from <c>AgentApplication.OnMessage</c> in that it only matches for the A2A channel.
/// Provide <paramref name="text"/> for an exact match, <paramref name="textRegex"/> for a pattern match, or neither to match any message.
/// <paramref name="text"/> and <paramref name="textRegex"/> are mutually exclusive.
/// The method must match the <see cref="A2ARouteHandler"/> delegate signature, which delivers a
/// strongly-typed <see cref="IA2ATurnContext"/> for the current turn.
/// <code>
/// // Match any A2A message
/// [A2AMessageRoute]
/// public async Task OnMessageAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle any A2A message
/// }
///
/// // Match a specific A2A message
/// [A2AMessageRoute("hello")]
/// public async Task OnHelloAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle "hello" message
/// }
///
/// // Match a text pattern
/// [A2AMessageRoute(textRegex: "he.*o")]
/// public async Task OnHelloPatternAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle messages matching pattern
/// }
/// </code>
/// </remarks>
/// <param name="text">The exact message text to match. Mutually exclusive with <paramref name="textRegex"/>. When both are omitted, all messages are matched.</param>
/// <param name="textRegex">A regular expression pattern matched against <see cref="IActivity.Text"/>. Mutually exclusive with <paramref name="text"/>.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. When no text filter is specified, defaults to <see cref="RouteRank.Last"/> so specific-text routes take priority.</param>
/// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
[RouteHandlerType(typeof(A2ARouteHandler))]
public class A2AMessageRouteAttribute(string text = null, string textRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var a2aHandler = (A2ARouteHandler)RouteAttributeHelper.CreateMatchingHandlerDelegate(app, method, GetType());
        var routeHandler = HandlerUtils.WrapHandler(a2aHandler);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var builder = MessageRouteBuilder.Create().WithText(text).WithHandler(routeHandler).AsAgentic(isAgenticOnly).WithOrderRank(rank).WithChannelId(Channels.A2A);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
        else if (!string.IsNullOrWhiteSpace(textRegex))
        {
            var builder = MessageRouteBuilder.Create().WithText(new Regex(textRegex)).WithHandler(routeHandler).AsAgentic(isAgenticOnly).WithOrderRank(rank).WithChannelId(Channels.A2A);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
        else
        {
            var builder = TypeRouteBuilder.Create().WithType(ActivityTypes.Message).WithHandler(routeHandler).AsAgentic(isAgenticOnly).WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank).WithChannelId(Channels.A2A);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }
}
