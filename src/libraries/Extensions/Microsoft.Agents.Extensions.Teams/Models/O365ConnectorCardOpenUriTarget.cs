﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// O365 connector card OpenUri target.
    /// </summary>
    public class O365ConnectorCardOpenUriTarget
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="O365ConnectorCardOpenUriTarget"/> class.
        /// </summary>
        public O365ConnectorCardOpenUriTarget()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="O365ConnectorCardOpenUriTarget"/> class.
        /// </summary>
        /// <param name="os">Target operating system. Possible values include:
        /// 'default', 'iOS', 'android', 'windows'.</param>
        /// <param name="uri">Target url.</param>
        public O365ConnectorCardOpenUriTarget(string os = default, string uri = default)
        {
            Os = os;
            Uri = uri;
        }

        /// <summary>
        /// Gets or sets target operating system. Possible values include:
        /// 'default', 'iOS', 'android', 'windows'.
        /// </summary>
        /// <value>The target operating system.</value>
        public string Os { get; set; }

        /// <summary>
        /// Gets or sets target URL.
        /// </summary>
        /// <value>The target URL.</value>
        public string Uri { get; set; }
    }
}
