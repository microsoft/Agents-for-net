// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Settings for the Entra SDK Sidecar user authorization handler.
    /// </summary>
    public class EntraSidecarSettings
    {
        /// <summary>
        /// The base URL of the Entra SDK Sidecar (e.g., "http://localhost:5000").
        /// </summary>
        public string SidecarBaseUrl { get; set; }

        /// <summary>
        /// The configured downstream API service name in the sidecar.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Optional scope overrides for the token request.
        /// </summary>
        public IList<string> Scopes { get; set; }

        /// <summary>
        /// Optional alternate connection name for retrieving the inbound user token.
        /// If null, the default connection for the incoming request is used.
        /// </summary>
        public string AlternateBlueprintConnectionName { get; set; }
    }
}
