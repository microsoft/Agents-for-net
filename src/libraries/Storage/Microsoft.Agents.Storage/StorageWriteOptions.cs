// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// Options that control write behavior for storage providers.
    /// </summary>
    public record StorageWriteOptions
    {
        /// <summary>
        /// If true, the write operation will only succeed if the item does not already exist in storage.
        /// </summary>
        /// <remarks>
        /// This is useful for scenarios where you want to ensure that you are creating a new item
        /// and do not want to overwrite any existing data. If the item already exists, the write
        /// operation will fail with an error.
        ///
        /// The default value is false, meaning that the write operation will overwrite existing items.
        /// </remarks>
        public bool IfNotExists { get; set; }
    }
}
