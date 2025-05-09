// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.App
{
    public class TeamsApplicationOptions : AgentApplicationOptions
    {
        public TeamsApplicationOptions(IStorage storage) : base(storage) { }

        public TeamsApplicationOptions(
            IServiceProvider sp,
            IConfiguration configuration,
            IChannelAdapter channelAdapter,
            IStorage storage,
            IList<IInputFileDownloader> fileDownloaders = null,
            ITranscriptStore transcriptStore = null,
            AutoSignInSelector autoSignInSelector = null,
            string configKey = "AgentApplication") : base(sp, configuration, channelAdapter, storage, fileDownloaders, transcriptStore, autoSignInSelector, configKey)
        {
        }

        /// <summary>
        /// Optional. Options used to customize the processing of Task Modules requests.
        /// </summary>
        public TaskModulesOptions? TaskModules { get; set; }
    }
}
