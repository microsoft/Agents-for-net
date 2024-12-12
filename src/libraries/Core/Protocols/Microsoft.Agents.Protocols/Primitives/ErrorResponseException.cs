// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Protocols.Primitives
{
    /// <summary>
    /// Exception thrown for an invalid response with ErrorResponse
    /// information.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ErrorResponseException"/> class.
    /// </remarks>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public partial class ErrorResponseException(string message, System.Exception innerException = null) : Exception(message, innerException)
    {
        /// <summary>
        /// Gets or sets the body object.
        /// </summary>
        /// <value>The body.</value>
        public ErrorResponse Body { get; set; }
    }
}
