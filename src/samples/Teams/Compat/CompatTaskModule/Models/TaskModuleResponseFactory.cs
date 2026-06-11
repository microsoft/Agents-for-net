// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;

namespace CompatTaskModule.Models
{
    public static class TaskModuleResponseFactory
    {
        public static Microsoft.Teams.Api.TaskModules.Response CreateResponse(string message)
        {
            return new Microsoft.Teams.Api.TaskModules.Response
            {
                Task = new Microsoft.Teams.Api.TaskModules.MessageTask(message)
            };
        }

        public static Microsoft.Teams.Api.TaskModules.Response CreateResponse(Microsoft.Teams.Api.TaskModules.TaskInfo taskInfo)
        {
            return new Microsoft.Teams.Api.TaskModules.Response
            {
                Task = new Microsoft.Teams.Api.TaskModules.ContinueTask(taskInfo)
            };
        }

        public static Microsoft.Teams.Api.TaskModules.Response ToTaskModuleResponse(this Microsoft.Teams.Api.TaskModules.TaskInfo taskInfo)
        {
            return CreateResponse(taskInfo);
        }
    }
}
