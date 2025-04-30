﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// Defines the interface for a storage layer.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Reads storage items from storage.
        /// </summary>
        /// <param name="keys">keys of the <see cref="IStoreItem"/> objects to read.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// the items read, indexed by key.</remarks>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="WriteAsync(IDictionary{string, object}, CancellationToken)"/>
        Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads storage items from storage.
        /// </summary>
        /// <typeparam name="TStoreItem">The type of item to get from storage.</typeparam>
        /// <param name="keys">keys of the <see cref="IStoreItem"/> objects to read.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// the items read, indexed by key.</remarks>
        Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class;

        /// <summary>
        /// Writes storage items to storage.
        /// </summary>
        /// <param name="changes">The items to write, indexed by key.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes storage items to storage.
        /// </summary>
        /// <typeparam name="TStoreItem">The type of item to write to storage.</typeparam>
        /// <param name="changes">The items to write, indexed by key.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class;

        /// <summary>
        /// Deletes storage items from storage.
        /// </summary>
        /// <param name="keys">keys of the <see cref="IStoreItem"/> objects to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        /// <seealso cref="WriteAsync(IDictionary{string, object}, CancellationToken)"/>
        Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Exposes an ETag for concurrency control.
    /// </summary>
    public interface IStoreItem
    {
        /// <summary>
        /// Gets or sets the ETag for concurrency control.
        /// </summary>
        /// <value>The concurrency control ETag.</value>
        string ETag { get; set; }
    }
}
