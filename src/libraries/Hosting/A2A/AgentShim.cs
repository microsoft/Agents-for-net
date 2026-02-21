// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

/// <summary>
/// Provides a shim to connect an IAgent with an ITaskManager for A2A operations.
/// </summary>
/// <remarks>
/// This exists to:<br/>
/// - Provide context (requestId, identity, agent) to task manager event handlers.<br/>
/// - Intercept specific options to adjust for bug (CancelTaskAsync not returning updated task).<br/>
/// - Provide a mechanism to close internal Channel to unblock http request.
/// </remarks>
internal class AgentShim
{
    private readonly TaskManagerWrapper _taskManager;

    public AgentShim(
        string requestId,
        ClaimsIdentity identity,
        IAgent agent,
        ITaskStore taskStore,
        Func<string, ClaimsIdentity, IAgent, ITaskManager, AgentTask, CancellationToken, Task> onTaskCreated,
        Func<string, ClaimsIdentity, IAgent, ITaskManager, AgentTask, CancellationToken, Task> onTaskUpdated,
        Func<string, ClaimsIdentity, IAgent, ITaskManager, AgentTask, CancellationToken, Task> onCancel)
    {
        _taskManager = new TaskManagerWrapper(new TaskManager(taskStore: taskStore))
        {
            OnTaskCreated = (task, ct) => onTaskCreated(requestId, identity, agent, _taskManager, task, ct),
            OnTaskUpdated = (task, ct) => onTaskUpdated(requestId, identity, agent, _taskManager, task, ct),
            OnTaskCancelled = (task, ct) => onCancel(requestId, identity, agent, _taskManager, task, ct)
        };
    }

    public ITaskManager GetTaskManager()
    {
        return _taskManager;
    }
}
