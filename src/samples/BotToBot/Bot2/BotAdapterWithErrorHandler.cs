// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;

namespace Bot2
{
    public class BotAdapterWithErrorHandler : CloudAdapter
    {
        private readonly ILogger _logger;

        public BotAdapterWithErrorHandler(IChannelServiceClientFactory channelServiceClientFactory, IActivityTaskQueue activityTaskQueue, ILogger<IBotHttpAdapter> logger)
            : base(channelServiceClientFactory, activityTaskQueue)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            OnTurnError = HandleTurnError;
        }

        private async Task HandleTurnError(ITurnContext turnContext, Exception exception)
        {
            // Log any leaked exception from the application.
            _logger.LogError(exception, "[OnTurnError] unhandled error : {ExceptionMessage}", exception.Message);

            await SendErrorMessageAsync(turnContext, exception);
            await SendEoCToParentAsync(turnContext, exception);
        }

        private async Task SendErrorMessageAsync(ITurnContext turnContext, Exception exception)
        {
            try
            {
                // Send a message to the user.
                var errorMessageText = "The skill encountered an error or bug.";
                var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput.ToString());
                await turnContext.SendActivityAsync(errorMessage);

                errorMessageText = "To continue to run this bot, please fix the bot source code.";
                errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput.ToString());
                await turnContext.SendActivityAsync(errorMessage);

                // Send a trace activity, which will be displayed in the Bot Framework Emulator.
                // Note: we return the entire exception in the value property to help the developer;
                // this should not be done in production.
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.ToString(), "https://www.botframework.com/schemas/error", "TurnError");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception caught in SendErrorMessageAsync : {ExceptionMessage}", ex.Message);
            }
        }

        private async Task SendEoCToParentAsync(ITurnContext turnContext, Exception exception)
        {
            try
            {
                // Send an EndOfConversation activity to the caller with the error to end the conversation,
                // and let the caller decide what to do.
                var endOfConversation = Activity.CreateEndOfConversationActivity();
                endOfConversation.Code = "SkillError";
                endOfConversation.Text = exception.Message;
                await turnContext.SendActivityAsync(endOfConversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception caught in SendEoCToParentAsync: {ExceptionMessage}", ex.Message);
            }
        }
    }
}
