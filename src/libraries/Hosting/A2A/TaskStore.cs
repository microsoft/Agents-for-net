// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Storage;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    internal class TaskStore(IStorage storage) : ITaskStore
    {
        public async Task<AgentTask> CreateOrContinueTaskAsync(string contextId, string taskId, TaskState state = TaskState.Working, Message message = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(nameof(taskId), "Task ID cannot be null or empty.");

            AgentTask task;

            try
            {
                task = await GetTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
                task.Status = new Protocol.TaskStatus() { State = state, Timestamp = DateTimeOffset.UtcNow };
                task.History = AppendMessage(task.History, message);
            }
            catch (KeyNotFoundException)
            {
                task = new AgentTask() { ContextId = contextId, Id = taskId, Status = new Protocol.TaskStatus() { State = state, Timestamp = DateTimeOffset.UtcNow }, History = AppendMessage(default, message) };
            }

            return await UpdateTaskAsync(task, cancellationToken).ConfigureAwait(false);
        }

        public async Task<AgentTask> UpdateTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(task), "Task cannot be null.");

            if (task.Id == null)
            {
                throw new ArgumentException("Task must have a Id to update the task.", nameof(task));
            }

            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<AgentTask> UpdateTaskAsync(TaskArtifactUpdateEvent artifactUpdate, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(artifactUpdate), "TaskArtifactUpdateEvent cannot be null.");

            var task = await GetTaskAsync(artifactUpdate.TaskId, cancellationToken).ConfigureAwait(false);

            if (artifactUpdate.Append.HasValue && (bool)artifactUpdate.Append)
            {
                throw new NotImplementedException("Artifact Append not supported yet");
            }
            else
            {
                task.Artifacts = AddArtifact(task, artifactUpdate.Artifact);
            }

            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<AgentTask> UpdateTaskAsync(TaskStatusUpdateEvent statusUpdate, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(statusUpdate), "TaskStatusUpdateEvent cannot be null.");

            var task = await GetTaskAsync(statusUpdate.TaskId, cancellationToken).ConfigureAwait(false);
            task.Status = statusUpdate.Status;

            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<AgentTask> UpdateTaskAsync(Message message, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(nameof(message), "Message cannot be null.");

            var task = await GetTaskAsync(message.TaskId, cancellationToken).ConfigureAwait(false);
            task.History = AppendMessage(task.History, message);

            await storage.WriteAsync(new Dictionary<string, object> { { GetKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
            return task;
        }

        public async Task<AgentTask> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(nameof(taskId), "Task ID cannot be null or empty.");

            var key = GetKey(taskId);
            var items = await storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out var existingItem) && existingItem is AgentTask existingTask)
            {
                return existingTask;
            }

            throw new KeyNotFoundException($"Task with ID '{taskId}' not found.");
        }

        private static string GetKey(string taskId)
        {
            return $"task/{taskId}";
        }

        private static ImmutableArray<Message>? AppendMessage(ImmutableArray<Message>? h, Message m)
        {
            if (m == null)
            {
                return h;
            }

            if (h.HasValue)
            {
                h = h.Value.Add(m);
            }
            else
            {
                h = [m];
            }

            return h;
        }

        private static ImmutableArray<Artifact>? AddArtifact(AgentTask t, Artifact a)
        {
            var artifacts = t.Artifacts;

            if (artifacts.HasValue)
            {
                var artifact = artifacts.Value.Where(t => t.ArtifactId == a.ArtifactId).First();

                artifacts = artifact != null
                    ? artifacts.Value.Replace(artifact, a)
                    : artifacts.Value.Add(a);
            }
            else
            {
                artifacts = [a];
            }

            return artifacts;
        }
    }
}
