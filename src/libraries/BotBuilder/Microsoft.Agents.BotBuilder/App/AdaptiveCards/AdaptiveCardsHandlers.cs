// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.App.AdaptiveCards
{
    /// <summary>
    /// Parameters passed to AdaptiveCards.OnSearch() handler.
    /// </summary>
    public class AdaptiveCardsSearchParams
    {
        /// <summary>
        /// The query text.
        /// </summary>
        public string QueryText { get; set; }

        /// <summary>
        /// The dataset to search.
        /// </summary>
        public string Dataset { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveCardsSearchParams"/> class.
        /// </summary>
        /// <param name="queryText">The query text.</param>
        /// <param name="dataset">The dataset to search.</param>
        public AdaptiveCardsSearchParams(string queryText, string dataset)
        {
            this.QueryText = queryText;
            this.Dataset = dataset;
        }
    }

    /// <summary>
    /// Individual result returned from AdaptiveCards.OnSearch() handler.
    /// </summary>
    public class AdaptiveCardsSearchResult
    {
        /// <summary>
        /// The title of the result.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The subtitle of the result.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveCardsSearchResult"/> class.
        /// </summary>
        /// <param name="title">The title of the result.</param>
        /// <param name="value">The subtitle of the result.</param>
        public AdaptiveCardsSearchResult(string title, string value)
        {
            this.Title = title;
            this.Value = value;
        }
    }

    /// <summary>
    /// Function for handling Adaptive Card Action.Execute events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data associated with the action.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of AdaptiveCardInvokeResponse, which can be created using <see cref="AdaptiveCardInvokeResponseFactory"/>.</returns>
    public delegate Task<AdaptiveCardInvokeResponse> ActionExecuteHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Adaptive Card Action.Submit events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data associated with the action.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task ActionSubmitHandler(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Adaptive Card dynamic search events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="query">The query arguments.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A list of AdaptiveCardsSearchResult.</returns>
    public delegate Task<IList<AdaptiveCardsSearchResult>> SearchHandlerAsync(ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken cancellationToken);
}
