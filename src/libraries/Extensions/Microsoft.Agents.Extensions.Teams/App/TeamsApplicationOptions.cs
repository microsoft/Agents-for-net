// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.AdaptiveCards;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.App
{
    public class TeamsApplicationOptions : AgentApplicationOptions
    {
        public TeamsApplicationOptions() : base() { }

        public TeamsApplicationOptions(IServiceProvider sp, IConfiguration configuration, IChannelAdapter channelAdapter, IStorage storage, UserAuthorizationOptions authOptions = null, AdaptiveCardsOptions cardOptions = null, ILoggerFactory loggerFactory = null, IList<IInputFileDownloader> fileDownloaders = null, string configurationSection = "AgentApplication") 
            : base(sp, configuration, channelAdapter, storage, authOptions, cardOptions, loggerFactory, fileDownloaders, configurationSection)
        {
        }

        /// <summary>
        /// Optional. Options used to customize the processing of Task Modules requests.
        /// </summary>
        public TaskModulesOptions? TaskModules { get; set; }
    }
}
