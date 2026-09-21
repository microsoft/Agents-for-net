// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// Defines the version 2 storage contract for portable key-value operations.
    /// </summary>
    public interface IStorageV2 : IStorage
    {
        /// <summary>
        /// Reads items from storage and returns an explicit result for each requested key.
        /// </summary>
        /// <param name="keys">The keys to read.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<IReadOnlyDictionary<string, StorageReadResult>> ReadAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes typed items to storage and returns an explicit result for each requested write.
        /// </summary>
        /// <typeparam name="TValue">The value type to write.</typeparam>
        /// <param name="changes">The keyed items to write.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, CancellationToken cancellationToken = default) where TValue : class;

        /// <summary>
        /// Writes typed items to storage and returns an explicit result for each requested write.
        /// </summary>
        /// <typeparam name="TValue">The value type to write.</typeparam>
        /// <param name="changes">The keyed items to write.</param>
        /// <param name="options">Optional settings for the write operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, StorageWriteOptions options, CancellationToken cancellationToken = default) where TValue : class;

        /// <summary>
        /// Deletes items from storage and returns an explicit result for each requested delete.
        /// </summary>
        /// <param name="keys">The keys to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes items from storage and returns an explicit result for each requested delete.
        /// </summary>
        /// <param name="keys">The keys to delete.</param>
        /// <param name="options">Optional settings for the delete operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, StorageDeleteOptions options, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents write settings for a batch of storage operations.
    /// </summary>
    public sealed class StorageWriteOptions
    {
        /// <summary>
        /// Gets or sets the write mode applied to all items in the operation.
        /// </summary>
        public StorageWriteMode Mode { get; set; } = StorageWriteMode.Upsert;

        /// <summary>
        /// Gets or sets the provider-specific version token that must match before the write is applied.
        /// This is typically the <see cref="StorageReadResult.Version"/> or <see cref="StorageWriteResult.Version"/>
        /// returned by a prior V2 operation. For providers that use ETags, this maps to the underlying ETag value.
        /// </summary>
        public string ExpectedVersion { get; set; }
    }

    /// <summary>
    /// Represents delete settings for a batch of storage operations.
    /// </summary>
    public sealed class StorageDeleteOptions
    {
        /// <summary>
        /// Gets or sets the provider-specific version token that must match before the delete is applied.
        /// This is typically the <see cref="StorageReadResult.Version"/> or <see cref="StorageWriteResult.Version"/>
        /// returned by a prior V2 operation. For providers that use ETags, this maps to the underlying ETag value.
        /// </summary>
        public string ExpectedVersion { get; set; }
    }

    /// <summary>
    /// Represents the result of a storage read.
    /// </summary>
    public class StorageReadResult
    {
        /// <summary>
        /// Gets or sets the key that was read.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the read status.
        /// </summary>
        public StorageOperationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the value returned from storage.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets the current provider-specific version token returned from storage.
        /// For providers that use ETags, this maps to the underlying ETag value.
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// Represents the result of a storage write.
    /// </summary>
    public class StorageWriteResult
    {
        /// <summary>
        /// Gets or sets the key that was written.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the write status.
        /// </summary>
        public StorageOperationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the current provider-specific version token returned from storage.
        /// For providers that use ETags, this maps to the underlying ETag value.
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// Represents the result of a storage delete.
    /// </summary>
    public class StorageDeleteResult
    {
        /// <summary>
        /// Gets or sets the key that was deleted.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the delete status.
        /// </summary>
        public StorageOperationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the provider-specific version token returned from storage when available.
        /// For providers that use ETags, this maps to the underlying ETag value.
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// Defines the write mode for a storage write request.
    /// </summary>
    public enum StorageWriteMode
    {
        /// <summary>
        /// Insert or replace the target item.
        /// </summary>
        Upsert,

        /// <summary>
        /// Create the target item only when it does not already exist.
        /// </summary>
        CreateOnly,

        /// <summary>
        /// Replace the target item only when it already exists.
        /// </summary>
        Replace,
    }

    /// <summary>
    /// Defines the outcome of a storage operation.
    /// </summary>
    public enum StorageOperationStatus
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The target item was not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// The operation conflicted with the current storage state.
        /// </summary>
        Conflict,

        /// <summary>
        /// The requested condition was not satisfied.
        /// </summary>
        ConditionNotMet,
    }
}