// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.App.UserAuth;
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
        public TeamsApplicationOptions(IStorage storage) : base(storage) { }

        public TeamsApplicationOptions(IServiceProvider sp, IConfiguration configuration, IChannelAdapter channelAdapter, IStorage storage, UserAuthorizationOptions authOptions = null, AdaptiveCardsOptions cardOptions = null, IList<IInputFileDownloader> fileDownloaders = null, string configurationSection = "AgentApplication") 
            : base(sp, configuration, channelAdapter, storage, authOptions, cardOptions, fileDownloaders, configurationSection)
        {
        }

        /// <summary>
        /// Optional. Options used to customize the processing of Task Modules requests.
        /// </summary>
        public TaskModulesOptions? TaskModules { get; set; }
    }
}
