﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using System.Collections.Generic;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Options for the <see cref="AgentApplication"/> class.
    /// </summary>
    public class AgentApplicationOptions
    {
        public AgentApplicationOptions() { }

        /// <summary>
        /// Creates AgentApplicationOptions from IConfiguration and DI.
        /// </summary>
        /// <code>
        /// "AgentApplication": {
        ///   "StartTypingTimer": false,
        ///   "RemoveRecipientMention": true,
        ///   "NormalizeMentions": true,
        ///   
        ///   "UserAuthorization": {  // omit to disable User Authorization
        ///     "Default": "graph",
        ///     "AutoSignIn": {true | false},
        ///     "Handlers": {
        ///       "graph": {
        ///         "Settings": {
        ///           "AzureBotOAuthConnectionName": null
        ///         }
        ///       }
        ///     }
        ///   },
        ///   
        ///   "AdaptiveCards" : {     // optional
        ///   }
        /// }
        /// </code>
        /// <param name="sp"></param>
        /// <param name="configuration"></param>
        /// <param name="channelAdapter"></param>
        /// <param name="storage">The IStorage used by UserAuthorization.</param>
        /// <param name="authOptions"></param>
        /// <param name="cardOptions"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="fileDownloaders"></param>
        /// <param name="configKey"></param>
        public AgentApplicationOptions(
            IServiceProvider sp,
            IConfiguration configuration, 
            IChannelAdapter channelAdapter, 
            IStorage storage = null, 
            UserAuthorizationOptions authOptions = null,
            AdaptiveCardsOptions cardOptions = null,
            ILoggerFactory loggerFactory = null,
            IList<IInputFileDownloader> fileDownloaders = null,
            string configKey = "AgentApplication") 
        { 
            Adapter = channelAdapter;
            TurnStateFactory = () => new TurnState(storage);  // Null storage will just create a TurnState with TempState.
            LoggerFactory = loggerFactory;

            var section = configuration.GetSection(configKey);
            StartTypingTimer = section.GetValue<bool>(nameof(StartTypingTimer), false);
            RemoveRecipientMention = section.GetValue<bool>(nameof(RemoveRecipientMention), true);
            NormalizeMentions = section.GetValue<bool>(nameof(NormalizeMentions), true);

            if (authOptions != null)
            {
                UserAuthorization = authOptions;
            }
            else if (section.GetSection("UserAuthorization").Exists())
            {
                UserAuthorization = new UserAuthorizationOptions(sp, configuration, storage, configKey: $"{configKey}:UserAuthorization");
            }

            section = section.GetSection("AdaptiveCards");
            if (section.Exists())
            {
                AdaptiveCards = cardOptions ?? section.Get<AdaptiveCardsOptions>();
            }

            // Can't get these from config at the moment
            FileDownloaders = fileDownloaders;
        }

        public IChannelAdapter? Adapter { get; set; }

        /// <summary>
        /// Optional. Options used to customize the processing of Adaptive Card requests.
        /// </summary>
        public AdaptiveCardsOptions? AdaptiveCards { get; set; }

        /// <summary>
        /// Optional. Factory used to create a custom turn state instance.
        /// </summary>
        public Func<ITurnState>? TurnStateFactory { get; set; }

        /// <summary>
        /// Optional. Array of input file download plugins to use.
        /// </summary>
        public IList<IInputFileDownloader>? FileDownloaders { get; set; }

        /// <summary>
        /// Optional. Logger factory that will be used in this application.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Optional. If true, the Agent will automatically remove mentions of the Agents name from incoming
        /// messages. Defaults to true.
        /// </summary>
        public bool RemoveRecipientMention { get; set; } = true;

        /// <summary>
        /// Optional. If true, the Agent will automatically normalize mentions across channels.
        /// Defaults to true.
        /// </summary>
        public bool NormalizeMentions { get; set; } = true;

        /// <summary>
        /// Optional. If true, the Agent will automatically start a typing timer when messages are received.
        /// This allows the Agent to automatically indicate that it's received the message and is processing
        /// the request. Defaults to true.
        /// </summary>
        public bool StartTypingTimer { get; set; } = false;

        /// <summary>
        /// Optional. Options used to enable user authorization for the application.
        /// </summary>
        public UserAuthorizationOptions UserAuthorization { get; set; }
    }
}