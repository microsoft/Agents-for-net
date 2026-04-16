// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="ScopeStorageOperation"/> that traces a storage write operation.
    /// </summary>
    public class ScopeWrite : ScopeStorageOperation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeWrite"/> class.
        /// </summary>
        /// <param name="keyCount">The number of keys being written.</param>
        public ScopeWrite(int keyCount) : base(Constants.ScopeWrite, Constants.OperationWrite, keyCount)
        {
        }
    }
}