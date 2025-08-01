// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.A2A.Protocol
{
    /// <summary>
    /// Defines the possible lifecycle states of a Task.
    /// </summary>
    public enum TaskState
    {
        Submitted,
        Working,
        InputRequired,
        Completed,
        Cancelled,
        Failed,
        Rejected,
        AuthRequired,
        Unknown,
    }
}