// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using Microsoft.Agents.BotBuilder.App.AdaptiveCards;
using Microsoft.Agents.BotBuilder.State;

namespace Microsoft.Agents.BotBuilder.App
{
    /// <summary>
    /// A builder class for simplifying the creation of an Application instance.
    /// </summary>
    public class ApplicationBuilder
    {
        /// <summary>
        /// The application's configured options.
        /// </summary>
        public ApplicationOptions Options { get; } = new();

        /// <summary>
        /// Configures the turn state factory to use for managing the bot's turn state.
        /// </summary>
        /// <param name="turnStateFactory">The turn state factory to use.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public ApplicationBuilder WithTurnStateFactory(Func<ITurnState> turnStateFactory)
        {
            Options.TurnStateFactory = turnStateFactory;
            return this;
        }

        /// <summary>
        /// Configures the Logger factory for the application
        /// </summary>
        /// <param name="loggerFactory">The Logger factory</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public ApplicationBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            Options.LoggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Configures the processing of Adaptive Card requests.
        /// </summary>
        /// <param name="adaptiveCardOptions">The options for Adaptive Cards.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public ApplicationBuilder WithAdaptiveCardOptions(AdaptiveCardsOptions adaptiveCardOptions)
        {
            Options.AdaptiveCards = adaptiveCardOptions;
            return this;
        }

        /// <summary>
        /// Configures the removing of mentions of the bot's name from incoming messages.
        /// Default state for removeRecipientMention is true
        /// </summary>
        /// <param name="removeRecipientMention">The boolean for removing recipient mentions.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public ApplicationBuilder SetRemoveRecipientMention(bool removeRecipientMention)
        {
            Options.RemoveRecipientMention = removeRecipientMention;
            return this;
        }

        /// <summary>
        /// Configures the typing timer when messages are received.
        /// Default state for startTypingTimer is true
        /// </summary>
        /// <param name="startTypingTimer">The boolean for starting the typing timer.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public ApplicationBuilder SetStartTypingTimer(bool startTypingTimer)
        {
            Options.StartTypingTimer = startTypingTimer;
            return this;
        }

        //TODO
        /*
        /// <summary>
        /// Configures authentication for the application.
        /// </summary>
        /// <param name="adapter">The bot adapter.</param>
        /// <param name="authenticationOptions">The options for authentication.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public ApplicationBuilder<TState> WithAuthentication(ChannelAdapter adapter, AuthenticationOptions<TState> authenticationOptions)
        {
            Options.Adapter = adapter;
            Options.Authentication = authenticationOptions;
            return this;
        }
        */

        /// <summary>
        /// Builds and returns a new Application instance.
        /// </summary>
        /// <returns>The Application instance.</returns>
        public Application Build()
        {
            return new Application(Options);
        }
    }
}
