// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using System.Collections.Generic;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.Storage.Transcript;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// TurnState for an Agent is created at the beginning of a Turn by invoking the specified factory delegate.
    /// </summary>
    /// <remarks>
    /// The default implementation of this is
    /// <code>
    ///    () => new TurnState(service.GetService&lt;IStorage&gt;()
    /// </code>
    /// If <c>IStorage</c> was not DI'd, an (effectively) singleton instance should be used:
    /// <code>
    ///    var storage = new MemoryStorage();  // MemoryStorage for local dev
    ///    services.AddTransient&lt;IAgent&gt;(sp =>
    ///    {
    ///        var options = new AgentApplicationOptions(storage);
    ///        var app = new AgentApplication(options);
    ///
    ///        ...
    ///
    ///        return app;
    ///    });
    /// </code>
    /// </remarks>
    /// <seealso cref="TurnState"/>
    /// See MemoryStorage, BlobsStorage, or CosmosDbStorage.
    public delegate ITurnState TurnStateFactory();

    /// <summary>
    /// Options for the <see cref="AgentApplication"/> class.  AgentApplicationOptions can be constructed
    /// via <c>IConfiguration</c> values or programmatically.
    /// </summary>
    /// <seealso cref="TurnStateFactory"/>
    /// <seealso cref="UserAuthorizationOptions"/>
    public class AgentApplicationOptions
    {
        /// <summary>
        /// Constructs AgentApplicationOptions programmatically.
        /// <code>
        ///   var options = new AgentApplicationOptions(storageInstance)
        ///   {
        ///     StartTypingTimer = true,
        ///     
        ///     ...
        ///     
        ///     UserAuthorization = new UserAuthorizationOptions()   // if required
        ///     {
        ///       ...
        ///     }
        ///     
        ///     AdaptiveCards = new AdaptiveCardsOptions()  // if required
        ///     {
        ///     }
        ///   };
        /// </code>
        /// <remarks>
        /// This will set the `TurnStateFactory` property with the default TurnStateFactory .
        /// </remarks>
        /// </summary>
        public AgentApplicationOptions(IStorage storage) : this(storage == null ? () => new TurnState() : () => new TurnState(storage))
        {
        }

        public AgentApplicationOptions(TurnStateFactory turnStateFactory)
        {
            TurnStateFactory = turnStateFactory;
        }

        /// <summary>
        /// Creates AgentApplicationOptions from IConfiguration and DI.
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
        /// Typically this is used by injection:
        /// <code>
        ///   builder.Services.AddSingleton&lt;AgentApplicationOptions&lt;();
        /// </code>
        /// Or by using the Hosting.AspNetCore extension
        /// <code>
        ///   builder.AddAgentApplicationOptions();
        /// </code>
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="configuration"></param>
        /// <param name="channelAdapter"></param>
        /// <param name="storage">The IStorage used by TurnState and User Authorization.</param>
        /// <param name="authOptions"></param>
        /// <param name="cardOptions"></param>
        /// <param name="fileDownloaders"></param>
        /// <param name="transcriptStore"></param>
        /// <param name="autoSignInSelector"></param>
        /// <param name="configKey"></param>
        public AgentApplicationOptions(
            IServiceProvider sp,
            IConfiguration configuration, 
            IChannelAdapter channelAdapter, 
            IStorage storage = null, 
            UserAuthorizationOptions authOptions = null,
            AdaptiveCardsOptions cardOptions = null,
            IList<IInputFileDownloader> fileDownloaders = null,
            ITranscriptStore transcriptStore = null,
            AutoSignInSelector autoSignInSelector = null,
            string configKey = "AgentApplication") 
        { 
            Adapter = channelAdapter;
            TurnStateFactory = () => new TurnState(storage ?? sp.GetService<IStorage>());  // Null storage will just create a TurnState with TempState.

            var section = configuration.GetSection(configKey);
            if (!section.Exists())
            {
                // This is to compensate for IConfiguration containing the class name as the section name.
                section = configuration.GetSection(nameof(AgentApplicationOptions));
            }

            StartTypingTimer = section.GetValue<bool>(nameof(StartTypingTimer), false);
            RemoveRecipientMention = section.GetValue<bool>(nameof(RemoveRecipientMention), true);
            NormalizeMentions = section.GetValue<bool>(nameof(NormalizeMentions), true);

            if (section.GetSection("UserAuthorization").Exists())
            {
                UserAuthorization = new UserAuthorizationOptions(sp, configuration, storage, configKey: $"{configKey}:UserAuthorization");
                if (autoSignInSelector != null)
                {
                    UserAuthorization.AutoSignIn = autoSignInSelector;
                }
            }

            section = section.GetSection("AdaptiveCards");
            if (section.Exists())
            {
                AdaptiveCards = section.Get<AdaptiveCardsOptions>();
            }

            // Can't get these from config at the moment
            FileDownloaders = fileDownloaders;
            TranscriptStore = transcriptStore;
        }

        /// <summary>
        /// The IChannelAdapter to use in cases on proactive.
        /// </summary>
        /// <remarks>
        /// An Adapter would be required to use IChannelAdapter.ContinueConversationAsync or IChannelAdapter.CreateConversation.
        /// </remarks>
        public IChannelAdapter? Adapter { get; set; }

        /// <summary>
        /// Optional. Options used to customize the processing of Adaptive Card requests.
        /// </summary>
        public AdaptiveCardsOptions? AdaptiveCards { get; set; }

        /// <summary>
        /// Optional. Factory used to create a custom turn state instance.
        /// </summary>
        /// <remarks>
        /// Not setting the TurnStateFactory would result in an in-memory <see cref="TurnState"/> that provides just TempState.  This could
        /// be appropriate for Agents not needing persisted state.
        /// <see cref="Microsoft.Agents.Builder.App.TurnStateFactory"/>
        /// </remarks>
        public TurnStateFactory TurnStateFactory { get; set; }

        /// <summary>
        /// Optional. Array of input file download plugins to use.
        /// </summary>
        public IList<IInputFileDownloader>? FileDownloaders { get; set; }

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

        /// <summary>
        /// The transcript store to send Activity logs to.
        /// </summary>
        public ITranscriptStore TranscriptStore { get; set; }
    }
}