// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Constants for adaptive card invoke names
    /// </summary>
    public class AdaptiveCardsInvokeNames
    {
        /// <summary>
        /// Action invoke name
        /// </summary>
        public static readonly string ACTION_INVOKE_NAME = "adaptiveCard/action";
    }

    /// <summary>
    /// AdaptiveCards class to enable fluent style registration of handlers related to Adaptive Cards.
    /// </summary>
    public partial class AdaptiveCard
    {
        private static readonly string ACTION_EXECUTE_TYPE = "Action.Execute";
        private static readonly string SEARCH_INVOKE_NAME = "application/search";
        private static readonly string DEFAULT_ACTION_SUBMIT_FILTER = "verb";

        private readonly AgentApplication _app;

        /// <summary>
        /// Creates a new instance of the AdaptiveCards class.
        /// </summary>
        /// <param name="app"></param> The top level application class to register handlers with.
        public AdaptiveCard(AgentApplication app)
        {
            this._app = app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events that will match on the verb using an exact string comparison.
        /// </summary>
        /// <param name="verb">The named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionExecute(string verb, ActionExecuteHandler<IInvokeActivity> handler)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));
            return OnActionExecute((string input) => string.Equals(verb, input), handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events that will match on the verb using a Regex pattern.
        /// </summary>
        /// <param name="verbPattern">Regular expression to match against the named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionExecute(Regex verbPattern, ActionExecuteHandler<IInvokeActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
            return OnActionExecute((string input) => verbPattern.IsMatch(input), handler);
        }

        private AgentApplication OnActionExecute(Func<string, bool> isMatch, ActionExecuteHandler<IInvokeActivity> handler)
        {
            return _app.AddRoute(InvokeRouteBuilder.Create()
                .WithName(ACTION_EXECUTE_TYPE)
                .WithSelector((ctx, ct) =>
                {
                    if (AdaptiveCardInvokeResponseFactory.TryValidateActionInvokeValue(ctx.Activity, ACTION_EXECUTE_TYPE, out AdaptiveCardInvokeValue invokeValue, out var response))
                    {
                        return Task.FromResult(isMatch(invokeValue.Action.Verb));
                    }
                    return Task.FromResult(false);
                })
                .WithHandler(async (turnContext, turnState, cancellationToken) =>
                {
                    if (AdaptiveCardInvokeResponseFactory.TryValidateActionInvokeValue(turnContext.Activity, ACTION_EXECUTE_TYPE, out AdaptiveCardInvokeValue invokeValue, out var response))
                    {
                        response = await handler(turnContext, turnState, invokeValue.Action.Data, cancellationToken);
                    }
                    var activity = Activity.CreateInvokeResponseActivity(response, response.StatusCode ?? (int)HttpStatusCode.OK);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                })
                .Build()
            );
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="verb">The named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionSubmit(string verb, ActionSubmitHandler<IMessageActivity> handler)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));
            return OnActionSubmit((string input) => string.Equals(verb, input), handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="verbPattern">Regular expression to match against the named action to be handled.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionSubmit(Regex verbPattern, ActionSubmitHandler<IMessageActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
            return OnActionSubmit((string input) => verbPattern.IsMatch(input), handler);
        }

        private AgentApplication OnActionSubmit(Func<string, bool> isMatch, ActionSubmitHandler<IMessageActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(MessageRouteBuilder.Create()
                .WithSelector((ctx, ct) =>
                {
                    if (ctx.Activity is IMessageActivity message)
                    {
                        string filter = _app.Options.AdaptiveCards?.ActionSubmitFilter ?? DEFAULT_ACTION_SUBMIT_FILTER;
                        JsonObject obj = ProtocolJsonSerializer.ToObject<JsonObject>(message.Value);
                        return Task.FromResult(
                            string.IsNullOrEmpty(message.Text)
                            && message.Value != null
                            && obj[filter] != null
                            && obj[filter]!.GetValueKind() == System.Text.Json.JsonValueKind.String
                            && isMatch(obj[filter]!.ToString()!));
                    }
                    else
                    {
                        return Task.FromResult(false);
                    }
                })
                .WithHandler(async (turnContext, turnState, cancellationToken) =>
                {
                    await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                })
                .Build()
            );
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="dataset">The dataset to be searched.</param>
        /// <param name="handler">Function to call when the search is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSearch(string dataset, SearchHandler<IInvokeActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(dataset, nameof(dataset));
            return OnSearch((string input) => string.Equals(dataset, input), handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="datasetPattern">Regular expression to match against the dataset to be searched.</param>
        /// <param name="handler">Function to call when the search is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSearch(Regex datasetPattern, SearchHandler<IInvokeActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(datasetPattern, nameof(datasetPattern));
            return OnSearch((string input) => datasetPattern.IsMatch(input), handler);
        }

        private AgentApplication OnSearch(Func<string, bool> isMatch, SearchHandler<IInvokeActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            return _app.AddRoute(InvokeRouteBuilder.Create()
                .WithName(SEARCH_INVOKE_NAME)
                .WithSelector((ctx, ct) => Task.FromResult(isMatch(ProtocolJsonSerializer.ToObject<AdaptiveCardSearchInvokeValue>((ctx.Activity as IInvokeActivity)?.Value)?.Dataset)))
                .WithHandler(async (turnContext, turnState, cancellationToken) =>
                {
                    if (AdaptiveCardInvokeResponseFactory.TryValidateSearchInvokeValue(turnContext.Activity, out var searchInvokeValue, out var response))
                    {
                        AdaptiveCardsSearchParams adaptiveCardsSearchParams = new(searchInvokeValue.QueryText, searchInvokeValue.Dataset ?? string.Empty);
                        Query<AdaptiveCardsSearchParams> query = new(searchInvokeValue.QueryOptions.Top, searchInvokeValue.QueryOptions.Skip, adaptiveCardsSearchParams);

                        IList<AdaptiveCardsSearchResult> results = await handler(turnContext, turnState, query, cancellationToken);
                        response = AdaptiveCardInvokeResponseFactory.SearchResponse(
                            new AdaptiveCardsSearchInvokeResponseValue
                            {
                                Results = results
                            }
                        );
                    }

                    var invokeResponse = Activity.CreateInvokeResponseActivity(response, response.StatusCode ?? (int)HttpStatusCode.OK);
                    await turnContext.SendActivityAsync(invokeResponse, cancellationToken);
                })
                .Build()
            );
        }

        private class AdaptiveCardsSearchInvokeResponseValue
        {
            public IList<AdaptiveCardsSearchResult>? Results { get; set; }
        }
    }
}
