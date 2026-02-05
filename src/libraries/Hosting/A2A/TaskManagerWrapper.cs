// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

internal class TaskManagerWrapper(TaskManager taskManager) : ITaskManager
{
    Func<MessageSendParams, CancellationToken, Task<A2AResponse>>? ITaskManager.OnMessageReceived { get => taskManager.OnMessageReceived; set { taskManager.OnMessageReceived = value; } }
    public Func<AgentTask, CancellationToken, Task> OnTaskCreated { get => taskManager.OnTaskCreated; set { taskManager.OnTaskCreated = value; } }
    public Func<AgentTask, CancellationToken, Task> OnTaskCancelled { get => taskManager.OnTaskCancelled; set { taskManager.OnTaskCancelled = value; } }
    public Func<AgentTask, CancellationToken, Task> OnTaskUpdated { get => taskManager.OnTaskUpdated; set { taskManager.OnTaskUpdated = value; } }
    public Func<string, CancellationToken, Task<AgentCard>> OnAgentCardQuery { get => taskManager.OnAgentCardQuery; set { taskManager.OnAgentCardQuery = value; } }

    public async Task<AgentTask?> CancelTaskAsync(TaskIdParams taskIdParams, CancellationToken cancellationToken = default)
    {
        // This is because TaskManager.CancelTaskAsync does not return the updated task when using a non-InMemoryTaskStore based TaskStore.
        await taskManager.CancelTaskAsync(taskIdParams, cancellationToken).ConfigureAwait(false);
        return await taskManager.GetTaskAsync(new TaskQueryParams() { Id = taskIdParams.Id, Metadata = taskIdParams.Metadata }, cancellationToken).ConfigureAwait(false);
    }

    public Task<AgentTask> CreateTaskAsync(string? contextId = null, string? taskId = null, CancellationToken cancellationToken = default)
    {
        return taskManager.CreateTaskAsync(contextId, taskId, cancellationToken);
    }

    public Task<TaskPushNotificationConfig?> GetPushNotificationAsync(GetTaskPushNotificationConfigParams? notificationConfigParams, CancellationToken cancellationToken = default)
    {
        return taskManager.GetPushNotificationAsync(notificationConfigParams, cancellationToken);
    }

    public Task<AgentTask?> GetTaskAsync(TaskQueryParams taskIdParams, CancellationToken cancellationToken = default)
    {
        return taskManager.GetTaskAsync(taskIdParams, cancellationToken);
    }

    public Task ReturnArtifactAsync(string taskId, Artifact artifact, CancellationToken cancellationToken = default)
    {
        return taskManager.ReturnArtifactAsync(taskId, artifact, cancellationToken);
    }

    public Task<A2AResponse?> SendMessageAsync(MessageSendParams messageSendParams, CancellationToken cancellationToken = default)
    {
        return taskManager.SendMessageAsync(messageSendParams, cancellationToken);
    }

    public IAsyncEnumerable<A2AEvent> SendMessageStreamingAsync(MessageSendParams messageSendParams, CancellationToken cancellationToken = default)
    {
        return taskManager.SendMessageStreamingAsync(messageSendParams, cancellationToken);
    }

    public Task<TaskPushNotificationConfig?> SetPushNotificationAsync(TaskPushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default)
    {
        return taskManager.SetPushNotificationAsync(pushNotificationConfig, cancellationToken);
    }

    public IAsyncEnumerable<A2AEvent> SubscribeToTaskAsync(TaskIdParams taskIdParams, CancellationToken cancellationToken = default)
    {
        return taskManager.SubscribeToTaskAsync(taskIdParams, cancellationToken);
    }

    public Task UpdateStatusAsync(string taskId, TaskState status, AgentMessage? message = null, bool final = false, CancellationToken cancellationToken = default)
    {
        return taskManager.UpdateStatusAsync(taskId, status, message, final, cancellationToken);
    }

    public void CloseStream(string taskId)
    {
        // This is some hackery to get a2a-dotnet to close the stream.  Otherwise the
        // request is blocked waiting for something that will never come.  We can't
        // know when to set final=true when responses are sent.
        // This will only work given that the TaskManager in A2AAdapter is transient.
        // This also means that tasks/resubscribe won't work (because of the transient TaskManager).
        if (taskManager.GetType().GetField("_taskUpdateEventEnumerators", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(taskManager) is ConcurrentDictionary<string, TaskUpdateEventEnumerator> enumerators)
        {
            if (enumerators.TryRemove(taskId, out var enumerator))
            {
                enumerator.Dispose();
            }
        }
    }
}
