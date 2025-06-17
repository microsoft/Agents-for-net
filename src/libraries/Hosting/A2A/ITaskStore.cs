// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    internal interface ITaskStore
    {
        Task<TaskResponse> CreateOrUpdateTaskAsync(string contextId, string taskId, TaskState state = TaskState.Unknown, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="KeyNotFoundException">Thrown when the task with the specified ID does not exist.</exception>"
        Task<TaskResponse> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);

        Task<TaskResponse> UpdateTaskAsync(TaskResponse task, CancellationToken cancellationToken = default);

        Task<TaskResponse> UpdateTaskAsync(TaskArtifactUpdateEvent artifactUpdate, CancellationToken cancellationToken = default);

        Task<TaskResponse> UpdateTaskAsync(TaskStatusUpdateEvent statusUpdate, CancellationToken cancellationToken = default);

        Task<TaskResponse> UpdateTaskAsync(Message message, CancellationToken cancellationToken = default);
    }
}
