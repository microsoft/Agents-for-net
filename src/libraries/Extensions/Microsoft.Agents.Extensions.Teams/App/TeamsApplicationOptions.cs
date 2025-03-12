// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Extensions.Teams.App
{
    public class TeamsApplicationOptions : AgentApplicationOptions
    {
        public TeamsApplicationOptions() : base() { }

        public TeamsApplicationOptions(IConfiguration configuration, IChannelAdapter channelAdapter, IStorage storage, ILoggerFactory loggerFactory = null, string configurationSection = "AgentApplicationOptions") : base(configuration, channelAdapter, storage, loggerFactory, configurationSection)
        {
        }

        /// <summary>
        /// Optional. Options used to customize the processing of Task Modules requests.
        /// </summary>
        public TaskModulesOptions? TaskModules { get; set; }
    }
}
