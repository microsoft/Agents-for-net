// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.A2A.Storage;

/// <summary>
/// Configures resource limits for <see cref="Microsoft.Agents.Extensions.A2A.Storage.BlobPushNotificationStore"/>.
/// </summary>
public sealed class BlobPushNotificationStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of registrations per task. Defaults to 100.
    /// </summary>
    public int MaxRegistrationsPerTask { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum serialized configuration size in bytes. Defaults to 64 KiB.
    /// </summary>
    public int MaxConfigurationBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum encrypted task-document size in bytes. Defaults to 8 MiB.
    /// </summary>
    public int MaxDocumentBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of conditional write attempts. Defaults to five.
    /// </summary>
    public int MaxWriteAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether HTTP loopback storage endpoints are allowed for local development.
    /// Defaults to <see langword="false"/>. Non-loopback endpoints always require HTTPS.
    /// </summary>
    public bool AllowInsecureLoopback { get; set; }
}
