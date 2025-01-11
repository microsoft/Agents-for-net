// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// Sharepoint Location object.
    /// </summary>
    public class Location 
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Location"/> class.
        /// </summary>
        public Location()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets latitutde of the location of type <see cref="int"/>.
        /// </summary>
        /// <value>This value is the latitude of the location.</value>
        public int Latitude { get; set; }

        /// <summary>
        /// Gets or Sets longitude of the location of type <see cref="int"/>.
        /// </summary>
        /// <value>This value is the longitude of the location.</value>
        public int Longitude { get; set; }

        /// <summary>
        /// Gets or Sets timestamp of the location of type <see cref="int"/>.
        /// </summary>
        /// <value>This value is the timestamp of the location.</value>
        public int Timestamp { get; set; }

        /// <summary>
        /// Gets or Sets accuracy of the location of type <see cref="int"/>.
        /// </summary>
        /// <value>This value is the accuracy of the location.</value>
        public int Accuracy { get; set; }
    }
}
