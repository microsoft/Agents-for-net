// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.Testing.Http
{
    /// <summary>A framework-neutral HTTP simulation arrangement or verification failure.</summary>
    public sealed class HttpSimulationException : Exception
    {
        /// <summary>Initializes an HTTP simulation failure with a sanitized diagnostic.</summary>
        /// <param name="message">The diagnostic message.</param>
        public HttpSimulationException(string message) : base(message)
        {
        }
    }
}
