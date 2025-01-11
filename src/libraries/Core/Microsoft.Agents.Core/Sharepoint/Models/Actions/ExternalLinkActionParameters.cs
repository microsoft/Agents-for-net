// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint parameters for an External Link action.
    /// </summary>
    public class ExternalLinkActionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalLinkActionParameters"/> class.
        /// </summary>
        public ExternalLinkActionParameters()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets a value indicating whether this is a teams deep link property of type <see cref="bool"/>. 
        /// </summary>
        /// <value>This value indicates whether this is a Teams Deep Link.</value>
        public bool IsTeamsDeepLink { get; set; }

        /// <summary>
        /// Gets or Sets the target of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is external link to navigate to.</value>
        public string Target { get; set; }
    }
}
