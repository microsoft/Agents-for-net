// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Delegate for handling Message Extension fetch task events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="action">The <see cref="Microsoft.Teams.Api.MessageExtensions.Action"/> data associated with the fetch task event.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.ActionResponse.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> FetchActionHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken);

/// <summary>
/// Delegate for handling Message Extension submitAction events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="action">The action data associated with the submit action.</param>
/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
/// <returns>A task that represents the asynchronous operation. The task result contains a Response object that will be sent to
/// the user.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> SubmitActionHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension message preview edit events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="activityPreview">The activity that's being previewed by the user.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.Response.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> MessagePreviewEditHandler(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension message preview send events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="activityPreview">The activity that's being previewed by the user.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task MessagePreviewSendHandler(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension query events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="query">The query data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.Response.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> QueryHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension selecting item events.
/// </summary>
/// <typeparam name="TData">The type of the <c>data</c> argument associated with the select item action.</typeparam>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="data">The data associated with the select item action.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.Response.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> SelectItemHandler<TData>(ITurnContext turnContext, ITurnState turnState, TData data, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension link unfurling events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="url">The URL that should be unfurled.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.Response.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> QueryLinkHandler(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension configuring query setting url events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.Response.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> QueryUrlSettingHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension configuring settings events.
/// </summary>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="query">The query that was submitted.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessageExtensions.Response.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> ConfigureSettingsHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension clicking card button events.
/// </summary>
/// <typeparam name="TData">The type of the <c>cardData</c> argument.</typeparam>
/// <param name="turnContext">The context for the current conversation turn.</param>
/// <param name="turnState">The state object that stores arbitrary data for this turn.</param>
/// <param name="cardData">The card data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task CardButtonClickedHandler<TData>(ITurnContext turnContext, ITurnState turnState, TData cardData, CancellationToken cancellationToken);
