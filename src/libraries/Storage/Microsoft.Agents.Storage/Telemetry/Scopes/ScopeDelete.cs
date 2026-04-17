// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="ScopeStorageOperation"/> that traces a storage delete operation.
    /// </summary>
    public class ScopeDelete : ScopeStorageOperation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeDelete"/> class.
        /// </summary>
        /// <param name="keyCount">The number of keys being deleted.</param>
        public ScopeDelete(int keyCount) : base(Constants.ScopeDelete, Constants.OperationDelete, keyCount)
        {
        }
    }
}