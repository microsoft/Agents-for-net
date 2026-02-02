// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Config;
using Microsoft.Teams.Api.O365;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Function for handling config events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="configData">The config data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of ConfigResponseBase.</returns>
    public delegate Task<ConfigResponse> ConfigHandlerAsync(ITurnContext turnContext, ITurnState turnState, object configData, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling file consent card activities.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="fileConsentCardResponse">The response representing the value of the invoke activity sent when the user acts on a file consent card.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task FileConsentHandler(ITurnContext turnContext, ITurnState turnState, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling O365 Connector Card Action activities.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="query">The O365 connector card HttpPOST invoke query.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task O365ConnectorCardActionHandler(ITurnContext turnContext, ITurnState turnState, ConnectorCardActionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling read receipt events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The read receipt data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns></returns>
    public delegate Task ReadReceiptHandler(ITurnContext turnContext, ITurnState turnState, JsonElement data, CancellationToken cancellationToken);
}
