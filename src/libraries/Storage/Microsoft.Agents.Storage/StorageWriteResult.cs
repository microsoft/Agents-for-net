// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// Represents the response of a storage write operation, containing the updated ETag.
    /// </summary>
    public class StorageWriteResponse : IStoreItem
    {
        /// <summary>
        /// Gets or sets the entity tag (ETag) assigned to the item after the write operation.
        /// </summary>
        public string ETag { get; set; }
    }
}