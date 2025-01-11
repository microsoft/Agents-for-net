// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// O365 connector card OpenUri action.
    /// </summary>
    public class O365ConnectorCardOpenUri : O365ConnectorCardActionBase
    {
        /// <summary>
        /// Content type to be used in the @type property.
        /// </summary>
        public new const string Type = "OpenUri";

        /// <summary>
        /// Initializes a new instance of the <see cref="O365ConnectorCardOpenUri"/> class.
        /// </summary>
        public O365ConnectorCardOpenUri()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="O365ConnectorCardOpenUri"/> class.
        /// </summary>
        /// <param name="type">Type of the action. Possible values include:
        /// 'ViewAction', 'OpenUri', 'HttpPOST', 'ActionCard'.</param>
        /// <param name="name">Name of the action that will be used as button
        /// title.</param>
        /// <param name="id">Action Id.</param>
        /// <param name="targets">Target os / urls.</param>
        public O365ConnectorCardOpenUri(string type = default, string name = default, string id = default, IList<O365ConnectorCardOpenUriTarget> targets = default)
            : base(type, name, id)
        {
            Targets = targets;
        }

        /// <summary>
        /// Gets or sets target OS/URLs.
        /// </summary>
        /// <value>The target OS/URLs.</value>
#pragma warning disable CA2227 // Collection properties should be read only (we can't change this without breaking compat).
        public IList<O365ConnectorCardOpenUriTarget> Targets { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
