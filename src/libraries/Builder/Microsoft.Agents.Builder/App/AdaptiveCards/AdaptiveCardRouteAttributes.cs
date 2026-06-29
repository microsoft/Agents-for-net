// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Attribute to define a route that handles Adaptive Card <c>Action.Execute</c> invoke activities, optionally
    /// matching a specific verb or verb pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for Adaptive Card Action.Execute events.
    /// Provide <paramref name="verb"/> for an exact match, <paramref name="verbRegex"/> for a pattern match, or
    /// neither to match any verb. <paramref name="verb"/> and <paramref name="verbRegex"/> are mutually exclusive.
    /// The method must match the <see cref="ActionExecuteHandler"/> delegate signature.
    /// <code>
    /// [AdaptiveCardActionExecuteRoute("doStuff")]
    /// public async Task&lt;AdaptiveCardInvokeResponse&gt; OnDoStuffAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
    /// {
    ///     // Handle Action.Execute with verb "doStuff"
    /// }
    /// </code>
    /// </remarks>
    /// <param name="verb">The exact verb to match. Mutually exclusive with <paramref name="verbRegex"/>. When both are omitted, all verbs are matched.</param>
    /// <param name="verbRegex">A regular expression pattern matched against the Action.Execute verb. Mutually exclusive with <paramref name="verb"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    [RouteHandlerType(typeof(ActionExecuteHandler))]
    public class AdaptiveCardActionExecuteRouteAttribute(string verb = null, string verbRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<ActionExecuteHandler>(app, method);
            var builder = ActionExecuteRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));

            if (!string.IsNullOrWhiteSpace(verb))
            {
                builder.WithVerb(verb);
            }
            else if (!string.IsNullOrWhiteSpace(verbRegex))
            {
                builder.WithVerb(new Regex(verbRegex));
            }

            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles Adaptive Card <c>Action.Submit</c> message activities, optionally
    /// matching a specific verb or verb pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for Adaptive Card Action.Submit events.
    /// Provide <paramref name="verb"/> for an exact match, <paramref name="verbRegex"/> for a pattern match, or
    /// neither to match any Action.Submit message. <paramref name="verb"/> and <paramref name="verbRegex"/> are
    /// mutually exclusive. When a verb is matched, the activity value field configured by the
    /// <c>actionSubmitFilter</c> option (defaults to <c>verb</c>) is used.
    /// The method must match the <see cref="ActionSubmitHandler"/> delegate signature.
    /// <code>
    /// [AdaptiveCardActionSubmitRoute("ok")]
    /// public async Task OnOkAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
    /// {
    ///     // Handle Action.Submit with verb "ok"
    /// }
    /// </code>
    /// </remarks>
    /// <param name="verb">The exact verb to match. Mutually exclusive with <paramref name="verbRegex"/>. When both are omitted, all Action.Submit messages are matched.</param>
    /// <param name="verbRegex">A regular expression pattern matched against the Action.Submit filter field. Mutually exclusive with <paramref name="verb"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    [RouteHandlerType(typeof(ActionSubmitHandler))]
    public class AdaptiveCardActionSubmitRouteAttribute(string verb = null, string verbRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<ActionSubmitHandler>(app, method);
            var builder = ActionSubmitRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));

            string filter = app.Options.AdaptiveCards?.ActionSubmitFilter;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                builder.WithFilter(filter);
            }

            if (!string.IsNullOrWhiteSpace(verb))
            {
                builder.WithVerb(verb);
            }
            else if (!string.IsNullOrWhiteSpace(verbRegex))
            {
                builder.WithVerb(new Regex(verbRegex));
            }

            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles Adaptive Card dynamic search (<c>application/search</c>) invoke
    /// activities, optionally matching a specific dataset or dataset pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for Adaptive Card search events.
    /// Provide <paramref name="dataset"/> for an exact match, <paramref name="datasetRegex"/> for a pattern match, or
    /// neither to match any dataset. <paramref name="dataset"/> and <paramref name="datasetRegex"/> are mutually
    /// exclusive. The method must match the <see cref="SearchHandler"/> delegate signature.
    /// <code>
    /// [AdaptiveCardSearchRoute("npm")]
    /// public async Task&lt;IList&lt;AdaptiveCardsSearchResult&gt;&gt; OnNpmSearchAsync(ITurnContext turnContext, ITurnState turnState, Query&lt;AdaptiveCardsSearchParams&gt; query, CancellationToken cancellationToken)
    /// {
    ///     // Handle search for dataset "npm"
    /// }
    /// </code>
    /// </remarks>
    /// <param name="dataset">The exact dataset to match. Mutually exclusive with <paramref name="datasetRegex"/>. When both are omitted, all datasets are matched.</param>
    /// <param name="datasetRegex">A regular expression pattern matched against the search dataset. Mutually exclusive with <paramref name="dataset"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    [RouteHandlerType(typeof(SearchHandler))]
    public class AdaptiveCardSearchRouteAttribute(string dataset = null, string datasetRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<SearchHandler>(app, method);
            var builder = SearchRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));

            if (!string.IsNullOrWhiteSpace(dataset))
            {
                builder.WithDataset(dataset);
            }
            else if (!string.IsNullOrWhiteSpace(datasetRegex))
            {
                builder.WithDataset(new Regex(datasetRegex));
            }

            app.AddRoute(builder.Build());
        }
    }
}
