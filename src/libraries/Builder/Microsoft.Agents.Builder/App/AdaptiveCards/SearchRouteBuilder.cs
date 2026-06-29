// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Provides a concrete builder for routing Adaptive Card dynamic search (<c>application/search</c>) invoke
    /// activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Routes built from this builder match invoke activities named <c>application/search</c> whose dataset matches
    /// the configured dataset or dataset pattern. When neither <see cref="WithDataset(string)"/> nor
    /// <see cref="WithDataset(Regex)"/> is called the route matches any dataset. A custom selector supplied via
    /// <see cref="WithSelector(RouteSelector)"/> is additive to the basic route matching (channel, activity type,
    /// invoke name, and dataset).
    /// </remarks>
    public class SearchRouteBuilder : RouteBuilderBase<SearchRouteBuilder>
    {
        private const string SearchInvokeName = "application/search";

        private string _dataset;
        private Regex _datasetPattern;

        /// <summary>
        /// Creates a new instance of the <see cref="SearchRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="SearchRouteBuilder"/>.</returns>
        public static SearchRouteBuilder Create()
        {
            return new SearchRouteBuilder();
        }

        public SearchRouteBuilder() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Configures the route to match only search activities whose dataset equals the specified value.
        /// </summary>
        /// <param name="dataset">The dataset to match. Comparison is exact.</param>
        /// <returns>The current <see cref="SearchRouteBuilder"/> instance for method chaining.</returns>
        public SearchRouteBuilder WithDataset(string dataset)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));

            if (_dataset != null || _datasetPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"SearchRouteBuilder.WithDataset({dataset})");
            }

            _dataset = dataset;
            return this;
        }

        /// <summary>
        /// Configures the route to match search activities whose dataset matches the specified pattern.
        /// </summary>
        /// <param name="datasetPattern">A regular expression used to match the dataset.</param>
        /// <returns>The current <see cref="SearchRouteBuilder"/> instance for method chaining.</returns>
        public SearchRouteBuilder WithDataset(Regex datasetPattern)
        {
            AssertionHelpers.ThrowIfNull(datasetPattern, nameof(datasetPattern));

            if (_dataset != null || _datasetPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"SearchRouteBuilder.WithDataset(Regex({datasetPattern}))");
            }

            _datasetPattern = datasetPattern;
            return this;
        }

        /// <summary>
        /// Sets a custom route selector that is additive to the basic search matching (channel, activity type, invoke
        /// name, and dataset).
        /// </summary>
        /// <param name="selector">The route selector that defines additional criteria for matching requests to the route. The supplied
        /// selector does not need to validate base route properties like ChannelId, Agentic, activity type, invoke
        /// name, or dataset.</param>
        /// <returns>The current <see cref="SearchRouteBuilder"/> instance with the specified selector applied.</returns>
        public override SearchRouteBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"SearchRouteBuilder.WithSelector()");
            }

            _route.Selector = selector;
            return this;
        }

        /// <summary>
        /// Configures the route to handle dynamic search events using the specified handler.
        /// </summary>
        /// <param name="handler">The handler to invoke when a matching search activity is received. Cannot be null.</param>
        /// <returns>The current <see cref="SearchRouteBuilder"/> instance for method chaining.</returns>
        public SearchRouteBuilder WithHandler(SearchHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, SearchInvokeName))
                {
                    throw new InvalidOperationException($"Unexpected AdaptiveCards.OnSearch() triggered for activity type: {turnContext.Activity.Type}");
                }

                if (AdaptiveCardInvokeResponseFactory.TryValidateSearchInvokeValue(turnContext.Activity, out var searchInvokeValue, out var response))
                {
                    AdaptiveCardsSearchParams adaptiveCardsSearchParams = new(searchInvokeValue.QueryText, searchInvokeValue.Dataset ?? string.Empty);
                    Query<AdaptiveCardsSearchParams> query = new(searchInvokeValue.QueryOptions.Top, searchInvokeValue.QueryOptions.Skip, adaptiveCardsSearchParams);

                    IList<AdaptiveCardsSearchResult> results = await handler(turnContext, turnState, query, cancellationToken).ConfigureAwait(false);

                    response = AdaptiveCardInvokeResponseFactory.SearchResponse(
                        new AdaptiveCardsSearchInvokeResponseValue
                        {
                            Results = results
                        }
                    );
                }

                var invokeResponse = Activity.CreateInvokeResponseActivity(response, response.StatusCode ?? (int)HttpStatusCode.OK);
                await turnContext.SendActivityAsync(invokeResponse, cancellationToken).ConfigureAwait(false);
            }

            _route.Handler = routeHandler;
            return this;
        }

        /// <summary>
        /// Returns the current builder instance.
        /// </summary>
        /// <remarks>Search routes always handle invoke activities, so the value of <paramref name="isInvoke"/> is ignored.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override SearchRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        protected override void PreBuild()
        {
            var existingSelector = _route.Selector;

            _route.Selector = async (context, ct) =>
            {
                if (!IsContextMatch(context, _route)
                    || !context.Activity.IsType(ActivityTypes.Invoke)
                    || !string.Equals(context.Activity.Name, SearchInvokeName))
                {
                    return false;
                }

                AdaptiveCardSearchInvokeValue searchInvokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardSearchInvokeValue>(context.Activity.Value);
                if (!IsDatasetMatch(searchInvokeValue?.Dataset))
                {
                    return false;
                }

                return existingSelector == null || await existingSelector(context, ct).ConfigureAwait(false);
            };
        }

        private bool IsDatasetMatch(string dataset)
        {
            if (_dataset != null)
            {
                return string.Equals(_dataset, dataset);
            }

            if (_datasetPattern != null)
            {
                return dataset != null && _datasetPattern.IsMatch(dataset);
            }

            return true;
        }

        private class AdaptiveCardsSearchInvokeResponseValue
        {
            public IList<AdaptiveCardsSearchResult> Results { get; set; }
        }
    }
}
