// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace WorkIQBriefingAgent;

internal sealed class UserTaskStore(IHttpContextAccessor httpContextAccessor) : ITaskStore
{
    private readonly ConcurrentDictionary<string, InMemoryTaskStore> _users = new(StringComparer.Ordinal);

    // Endpoint authentication does not scope a shared task store. Partition all native operations,
    // including GetTask and CancelTask, rather than checking ownership only inside the message handler.
    private InMemoryTaskStore Current => _users.GetOrAdd(
        SignedInUser.GetId(httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("Task operations require an authenticated request.")),
        _ => new InMemoryTaskStore());

    public Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        => Current.GetTaskAsync(taskId, cancellationToken);

    public Task SaveTaskAsync(string taskId, AgentTask task, CancellationToken cancellationToken = default)
        => Current.SaveTaskAsync(taskId, task, cancellationToken);

    public Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
        => Current.DeleteTaskAsync(taskId, cancellationToken);

    public Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default)
        => Current.ListTasksAsync(request, cancellationToken);
}
