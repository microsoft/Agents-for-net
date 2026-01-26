// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// Exception thrown when there is an ETag mismatch during storage operations.
    /// </summary>
    public class EtagException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EtagException"/> class.
        /// </summary>
        public EtagException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EtagException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public EtagException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EtagException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public EtagException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
