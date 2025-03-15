// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;

namespace Microsoft.Agents.Client
{
    public delegate Task BotResponseHandler(ITurnContext turnContext, ITurnState turnState, BotConversationReference reference, IActivity botActivity, CancellationToken cancellationToken);

    /// <summary>
    /// Handles routing response from another Agent to AgentApplication.
    /// </summary>
    /// <code>
    /// class MyAgent : AgentApplication
    /// {
    ///     public MyAgent(AgentApplicationOptions options) : base(options)
    ///     {
    ///         BotResponses.OnBotReply(OnBotResponseAsync);
    ///     }
    ///     
    ///     private async Task OnBotResponseAsync(ITurnContext turnContext, ITurnState turnState, BotConversationReference reference, IActivity botActivity, CancellationToken cancellationToken)
    ///     {
    ///         // do something with the response
    ///     }
    /// }
    /// </code>
    public static class BotResponses
    {
        /// <summary>
        /// Provides a handler for when an bot sends an Activity when Activity.DeliverMode == `normal` (asynchronous HTTP POST back to the channel host.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="handler"></param>
        /// <param name="rank"></param>
        public static void OnBotReply(this AgentApplication app, BotResponseHandler handler, ushort rank = RouteRank.First)
        {
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                var botResponse = ProtocolJsonSerializer.ToObject<AdapterBotResponseHandler.BotReply>(turnContext.Activity.Value);
                await handler(turnContext, turnState, botResponse.BotConversationReference, botResponse.Activity, cancellationToken).ConfigureAwait(false);
            };

            app.OnActivity(
                (turnContext, CancellationToken) =>
                    Task.FromResult(string.Equals(ActivityTypes.Event, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AdapterBotResponseHandler.BotResponseEventName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase)),
                routeHandler,
                rank);
        }
    }
}
