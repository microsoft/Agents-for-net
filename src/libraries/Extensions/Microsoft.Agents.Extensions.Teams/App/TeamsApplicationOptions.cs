// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;

namespace Microsoft.Agents.Extensions.Teams.App
{
    public class TeamsApplicationOptions : AgentApplicationOptions
    {
        /// <summary>
        /// Optional. Options used to customize the processing of Task Modules requests.
        /// </summary>
        public TaskModulesOptions? TaskModules { get; set; }
    }
}
