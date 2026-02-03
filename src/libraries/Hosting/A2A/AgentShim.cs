// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    internal class AgentShim
    {
        private readonly TaskManager _taskManager;

        public AgentShim(
            string requestId, 
            ClaimsIdentity identity, 
            IAgent agent, 
            ITaskStore taskStore, 
            Func<string, ClaimsIdentity, IAgent, TaskManager, AgentTask, CancellationToken, Task> onTask,
            Func<string, ClaimsIdentity, IAgent, TaskManager, AgentTask, CancellationToken, Task> onCancel)
        {
            Task onExec(AgentTask task, CancellationToken ct) => onTask(requestId, identity, agent, _taskManager, task, ct);

            _taskManager = new TaskManager(taskStore: taskStore)
            {
                OnTaskCreated = onExec,
                OnTaskUpdated = onExec,
                OnTaskCancelled = (task, ct) => onCancel(requestId, identity, agent, _taskManager, task, ct)
            };
        }

        public TaskManager GetTaskManager()
        {
            return _taskManager;
        }
    }
}
