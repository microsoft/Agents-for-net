// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Exception thrown when the sidecar returns an error response.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SidecarRequestException"/> class.
    /// </remarks>
    /// <param name="statusCode">HTTP status code returned by the sidecar.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="problemDetails">Parsed ProblemDetails if available, otherwise null.</param>
    /// <param name="rawContent">Raw response content from the sidecar.</param>
    internal class SidecarRequestException(int statusCode, string message, SidecarProblemDetails problemDetails, string rawContent) : Exception(message)
    {

        /// <summary>HTTP status code returned by the sidecar.</summary>
        public int StatusCode { get; } = statusCode;

        /// <summary>Parsed ProblemDetails if available, otherwise null.</summary>
        public SidecarProblemDetails ProblemDetails { get; } = problemDetails;

        /// <summary>Raw response content from the sidecar.</summary>
        public string RawContent { get; } = rawContent;
    }
}
