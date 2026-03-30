// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Delegate for handling Message Extension submitAction events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="data">The data that was submitted.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionActionResponse.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> SubmitActionHandler(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken);

/// <summary>
/// Delegate for handling Message Extension submitAction events.
/// </summary>
/// <typeparam name="TData">The type of the data associated with the submit action.</typeparam>
/// <param name="turnContext">The context object for the current turn of the conversation. Provides information and operations for the ongoing
/// interaction.</param>
/// <param name="turnState">The state object for the current turn, used to access shared services and state information.</param>
/// <param name="data">The data payload submitted with the action. The type and structure depend on the specific submit action being
/// handled.</param>
/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
/// <returns>A task that represents the asynchronous operation. The task result contains a Response object that will be sent to
/// the user.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> SubmitActionHandler<TData>(ITurnContext turnContext, ITurnState turnState, TData data, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension botMessagePreview edit events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="activityPreview">The activity that's being previewed by the user.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionActionResponse.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Response> BotMessagePreviewEditHandler(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension botMessagePreview send events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="activityPreview">The activity that's being previewed by the user.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task BotMessagePreviewSendHandler(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension fetchTask events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of TaskModuleResponse.</returns>
public delegate Task<Microsoft.Teams.Api.TaskModules.Response> FetchTaskHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension query events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="query">The query parameters that were sent by the client.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionResult.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Result> QueryHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension selecting item events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="item">The item that was selected.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionResult.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Result> SelectItemHandler(ITurnContext turnContext, ITurnState turnState, object item, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension selecting item events.
/// </summary>
/// <typeparam name="TData">The type of the data associated with the select item action.</typeparam>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="item">The item that was selected.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionResult.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Result> SelectItemHandler<TData>(ITurnContext turnContext, ITurnState turnState, TData item, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension link unfurling events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="url">The URL that should be unfurled.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionResult.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Result> QueryLinkHandler(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension configuring query setting url events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>An instance of MessagingExtensionResult.</returns>
public delegate Task<Microsoft.Teams.Api.MessageExtensions.Result> QueryUrlSettingHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension configuring settings events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="query">The query that was submitted.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task ConfigureSettingsHandler(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension clicking card button events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="cardData">The card data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task CardButtonClickedHandler(ITurnContext turnContext, ITurnState turnState, object cardData, CancellationToken cancellationToken);

/// <summary>
/// Function for handling Message Extension clicking card button events.
/// </summary>
/// <param name="turnContext">A strongly-typed context object for this turn.</param>
/// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
/// <param name="cardData">The card data.</param>
/// <param name="cancellationToken">A cancellation token that can be used by other objects
/// or threads to receive notice of cancellation.</param>
/// <returns>A task that represents the work queued to execute.</returns>
public delegate Task CardButtonClickedHandler<TData>(ITurnContext turnContext, ITurnState turnState, TData cardData, CancellationToken cancellationToken);
