// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint parameters for a show location action.
    /// </summary>
    public class ShowLocationActionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShowLocationActionParameters"/> class.
        /// </summary>
        public ShowLocationActionParameters()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the location coordinates of type <see cref="Location"/>.
        /// </summary>
        /// <value>This value is the location to be shown.</value>
        public Location LocationCoordinates { get; set; }
    }
}
