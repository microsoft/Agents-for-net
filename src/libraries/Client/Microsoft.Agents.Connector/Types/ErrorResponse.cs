// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Connector.Types
{
    /// <summary> An HTTP API Error response. </summary>
    public class ErrorResponse
    {
        /// <summary> Initializes a new instance of ErrorResponse. </summary>
        public ErrorResponse()
        {
        }

        /// <summary> Initializes a new instance of ErrorResponse. </summary>
        /// <param name="error"> Object representing error information. </param>
        public ErrorResponse(Error error)
        {
            Error = error;
        }

        /// <summary> Object representing error information. </summary>
        public Error Error { get; set; }

        public override string ToString()
        {
            return $"Error Code:{Error.Code}, {Error.Message}";
        }

    }
}
