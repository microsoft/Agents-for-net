// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// Exception thrown when an item already exists in storage.
    /// </summary>
    public class ItemExistsException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemExistsException"/> class.
        /// </summary>
        public ItemExistsException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemExistsException"/> class with a specified error message.
        /// </summary>
        /// <param name="key">The key of the item that already exists.</param>
        public ItemExistsException(string key) : base(key)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemExistsException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="key">The key of the item that already exists.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ItemExistsException(string key, Exception innerException) : base(key, innerException)
        {
        }
    }
}