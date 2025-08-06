// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    internal class TaskStore(IStorage storage) : ITaskStore
    {
        public async Task<TaskResponse> CreateOrContinueTaskAsync(string contextId, string taskId, TaskState state = TaskState.Working, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(nameof(taskId), "Task ID cannot be null or empty.");

            TaskResponse task;

            try
            {
                task = await GetTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
                task = task with
                {
                    Status = new Protocol.TaskStatus() { State = state, Timestamp = DateTimeOffset.UtcNow },
                };
            }
            catch (KeyNotFoundException)
            {
                task = new TaskResponse() { ContextId = contextId, Id = taskId, Status = new Protocol.TaskStatus() { State = state, Timestamp = DateTimeOffset.UtcNow } };
            }

            return await UpdateTaskAsync(task, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TaskResponse> UpdateTaskAsync(TaskResponse task, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(task), "Task cannot be null.");

            if (task.Id == null)
            {
                throw new ArgumentException("Task must have a Id to update the task.", nameof(task));
            }

            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<TaskResponse> UpdateTaskAsync(TaskArtifactUpdateEvent artifactUpdate, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(artifactUpdate), "TaskArtifactUpdateEvent cannot be null.");

            var task = await GetTaskAsync(artifactUpdate.TaskId, cancellationToken).ConfigureAwait(false);
            if (artifactUpdate.Append.HasValue && (bool) artifactUpdate.Append)
            {
                System.Diagnostics.Trace.WriteLine("================ Artifact Append not supported yet ================");
            }
            else
            {
                task = task with
                {
                    Artifacts = task.Artifacts.HasValue ? ((ImmutableArray<Artifact>)task.Artifacts).Add(artifactUpdate.Artifact) : [artifactUpdate.Artifact]
                };
            }

            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<TaskResponse> UpdateTaskAsync(TaskStatusUpdateEvent statusUpdate, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(statusUpdate), "TaskStatusUpdateEvent cannot be null.");

            var task = await GetTaskAsync(statusUpdate.TaskId, cancellationToken).ConfigureAwait(false);
            task = task with
            {
                Status = statusUpdate.Status,
            };
            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<TaskResponse> UpdateTaskAsync(Message message, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(message), "Message cannot be null.");

            var task = await GetTaskAsync(message.TaskId, cancellationToken).ConfigureAwait(false);
            task = task with
            {
                History = task.History == null ? [message] : ImmutableArray<Message>.Empty.AddRange(task.History).Add(message),
            };
            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<TaskResponse> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(nameof(taskId), "Task ID cannot be null or empty.");

            var key = GetKey(taskId);
            var items = await storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out var existingItem) && existingItem is TaskResponse existingTask)
            {
                return existingTask;
            }

            throw new KeyNotFoundException($"Task with ID '{taskId}' not found.");
        }

        private static string GetKey(string taskId)
        {
            return $"task/{taskId}";
        }
    }
}
