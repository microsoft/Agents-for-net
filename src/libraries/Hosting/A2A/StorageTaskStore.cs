// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core;
using Microsoft.Agents.Storage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

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
    public async Task SaveTaskAsync(string taskId, AgentTask task, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(task, nameof(task));
        AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
        cancellationToken.ThrowIfCancellationRequested();

        task.Id ??= taskId;

        await _storage.WriteAsync(new Dictionary<string, object> { { GetTaskKey(task.Id), task } }, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
        cancellationToken.ThrowIfCancellationRequested();
        // TODO ETag DeleteTaskAsync
        return _storage.DeleteAsync([GetTaskKey(taskId)], cancellationToken);
    }

    public Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ListTasksResponse
        {
            Tasks = []
        });
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
