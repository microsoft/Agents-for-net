// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Linq;

namespace Microsoft.Agents.Hosting.A2A;

internal static class A2AModelExtensions
{
    public static bool IsTerminal(this AgentTask task)
    {
        return task.Status.State == TaskState.Completed
            || task.Status.State == TaskState.Canceled
            || task.Status.State == TaskState.Rejected
            || task.Status.State == TaskState.Failed;
    }

    public static AgentTask WithHistoryTrimmedTo(this AgentTask task, int? toLength)
    {
        if (!toLength.HasValue || toLength.Value < 0 || task.History.Value.Length <= 0 || task.History.Value.Length <= toLength.Value)
        {
            return task;
        }

        return new AgentTask
        {
            Id = task.Id,
            ContextId = task.ContextId,
            Status = task.Status,
            Artifacts = task.Artifacts,
            Metadata = task.Metadata,
            History = [.. task.History.Value.Skip(task.History.Value.Length - toLength.Value)],
        };
    }

    public static string ConversationId(this AgentTask task)
    {
        return task.Id;
    }
}