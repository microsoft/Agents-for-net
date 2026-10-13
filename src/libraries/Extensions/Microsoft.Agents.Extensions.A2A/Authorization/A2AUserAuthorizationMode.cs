// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.A2A.Authorization;

/// <summary>
/// Selects how an A2A authorization handler obtains its inbound credential.
/// </summary>
public enum A2AUserAuthorizationMode
{
    /// <summary>
    /// Uses the credential supplied with the original A2A request and contributes configured
    /// security metadata to the Agent Card.
    /// </summary>
    RequestToken,

    /// <summary>
    /// Requests a task-scoped credential after the Task enters
    /// <c>TASK_STATE_AUTH_REQUIRED</c>.
    /// </summary>
    InTask,
}
