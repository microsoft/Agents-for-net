// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    internal class StorageTaskStore : ITaskStore
    {
        private readonly IStorage _storage;

        public StorageTaskStore(IStorage storage)
        {
            AssertionHelpers.ThrowIfNull(storage, nameof(storage));
            _storage = storage;
        }

        public async Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
            cancellationToken.ThrowIfCancellationRequested();

            var key = GetTaskKey(taskId);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out var existingItem) && existingItem is AgentTask existingTask)
            {
                return existingTask;
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<TaskPushNotificationConfig?> GetPushNotificationAsync(string taskId, string notificationConfigId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(taskId, "Task ID cannot be null or empty.");
            AssertionHelpers.ThrowIfNullOrEmpty(notificationConfigId, "Notification ID cannot be null or empty.");
            cancellationToken.ThrowIfCancellationRequested();

            var pushNotifications = await GetPushNotificationsAsync(taskId, cancellationToken).ConfigureAwait(false);
            return pushNotifications.Where(config => config.PushNotificationConfig.Id == notificationConfigId).FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AgentTaskStatus> UpdateStatusAsync(string taskId, TaskState status, AgentMessage? message = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(taskId, "Task ID cannot be null or empty.");
            cancellationToken.ThrowIfCancellationRequested();

            // TODO ETag UpdateStatusAsync
            var key = GetTaskKey(taskId);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out var existingItem) && existingItem is AgentTask task)
            {
                if ((bool)!task?.IsTerminal())
                {
                    task.Status = new AgentTaskStatus
                    {
                        Message = message,
                        State = status,
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    await _storage.WriteAsync(new Dictionary<string, object> { { key, task } }, cancellationToken).ConfigureAwait(false);
                }

                return task.Status;
            }

            throw new A2AException("Task not found.", A2AErrorCode.TaskNotFound);
        }

        /// <inheritdoc />
        public async Task SetTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(task, "Task cannot be null.");
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(task.Id))
            {
                throw new A2AException("Invalid task ID", A2AErrorCode.InvalidParams);
            }

            await _storage.WriteAsync(new Dictionary<string, object> { { GetTaskKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SetPushNotificationConfigAsync(TaskPushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(pushNotificationConfig, "Task ID cannot be null or empty.");
            AssertionHelpers.ThrowIfNullOrEmpty(pushNotificationConfig.TaskId, "Task ID cannot be null or empty.");
            cancellationToken.ThrowIfCancellationRequested();

            // TODO ETag SetPushNotificationConfigAsync
            var key = GetPushKey(pushNotificationConfig.TaskId);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out var existingItem) && existingItem is PushNotifications existingConfigs)
            {
                existingConfigs.Configs.Add(pushNotificationConfig);
            }
            else
            {
                existingConfigs = new PushNotifications([pushNotificationConfig]);
            }

            items[key] = existingConfigs;
            await _storage.WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<TaskPushNotificationConfig>> GetPushNotificationsAsync(string taskId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
            cancellationToken.ThrowIfCancellationRequested();

            var key = GetPushKey(taskId);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(key, out var existingItem) && existingItem is PushNotifications existingConfigs)
            {
                return existingConfigs.Configs;
            }

            return [];
        }

        private static string GetTaskKey(string taskId)
        {
            return $"a2atask/{taskId}";
        }

        private static string GetPushKey(string taskId)
        {
            return $"a2apush/{taskId}";
        }

        internal class PushNotifications
        {
            public PushNotifications(List<TaskPushNotificationConfig> configs = null) 
            {
                Configs = configs ?? [];
            }

            public List<TaskPushNotificationConfig> Configs { get; set; } = [];
        }
    }
}
