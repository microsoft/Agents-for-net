// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Storage;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    internal class AgentShim
    {
        private readonly TaskManager _taskManager;

        public AgentShim(string requestId, ClaimsIdentity identity, IAgent agent, IStorage storage, Func<string, ClaimsIdentity, IAgent, TaskManager, AgentTask, CancellationToken, Task> onTask)
        {
            Task onExec(AgentTask task, CancellationToken ct) => onTask(requestId, identity, agent, _taskManager, task, ct);

            _taskManager = new TaskManager(taskStore: new InMemoryTaskStore()) // new StorageTaskStore(storage))
            {
                OnTaskCreated = onExec,
                //OnTaskUpdated = onExec
            };
        }

        public TaskManager GetTaskManager()
        {
            return _taskManager;
        }
    }
}
