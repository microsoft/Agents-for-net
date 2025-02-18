// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;

namespace Microsoft.Agents.Samples.Bots
{
    public class BotHostAdapterWithErrorHandler : CloudAdapter
    {
        private readonly ILogger _logger;
        private readonly IChannelHost _botsConfig;

        public BotHostAdapterWithErrorHandler(IChannelServiceClientFactory channelServiceClientFactory, IActivityTaskQueue activityTaskQueue, IChannelHost botsConfig, ILogger<IBotHttpAdapter> logger)
            : base(channelServiceClientFactory, activityTaskQueue, logger: logger)
        {
            _botsConfig = botsConfig ?? throw new ArgumentNullException(nameof(botsConfig));
            _logger = logger ?? NullLogger<IBotHttpAdapter>.Instance;

            OnTurnError = HandleTurnError;
        }

        private async Task HandleTurnError(ITurnContext turnContext, Exception exception)
        {
            // Log any leaked exception from the application.
            // NOTE: In production environment, you should consider logging this to
            // Azure Application Insights. Visit https://aka.ms/bottelemetry to see how
            // to add telemetry capture to your bot.
            _logger.LogError(exception, "[OnTurnError] unhandled error: {ExceptionMessage}", exception.Message);

            await SendErrorMessageAsync(turnContext, exception);
            await EndSkillConversationAsync(turnContext);
        }

        private async Task SendErrorMessageAsync(ITurnContext turnContext, Exception exception)
        {
            try
            {
                // Send a message to the user
                var errorMessageText = "The bot encountered an error or bug.";
                var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput.ToString());
                await turnContext.SendActivityAsync(errorMessage);

                errorMessageText = "To continue to run this bot, please fix the bot source code.";
                errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput.ToString());
                await turnContext.SendActivityAsync(errorMessage);

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.ToString(), "https://www.botframework.com/schemas/error", "TurnError");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception caught in SendErrorMessageAsync : {ExceptionMessage}", ex.Message);
            }
        }

        private Task EndSkillConversationAsync(ITurnContext turnContext)
        {
            if (_botsConfig == null)
            {
                return Task.CompletedTask;
            }

            return Task.CompletedTask;

            // Bot Framework only has the notion of a single active Skill.  In MCS SDK there could be multiple.
            /*
            try
            {
                // Inform the active skill that the conversation is ended so that it has
                // a chance to clean up.
                // Note: ActiveSkillPropertyName is set by the RooBot while messages are being
                // forwarded to a Skill.
                var activeSkill = await _conversationState.CreateProperty<BotInfo>(RootBot.ActiveSkillPropertyName).GetAsync(turnContext, () => null);
                if (activeSkill != null)
                {
                    var botId = _configuration["MicrosoftAppCredentials"];

                    var endOfConversation = Activity.CreateEndOfConversationActivity();
                    endOfConversation.Code = "RootSkillError";
                    endOfConversation.ApplyConversationReference(turnContext.Activity.GetConversationReference(), true);

                    using var client = _auth.CreateBotFrameworkClient();

                    await client.PostActivityAsync(botId, activeSkill.AppId, activeSkill.SkillEndpoint, _skillsConfig.BotHostEndpoint, endOfConversation.Conversation.Id, (Activity)endOfConversation, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception caught on attempting to send EndOfConversation : {ex}");
            }
            */
        }
    }
}
