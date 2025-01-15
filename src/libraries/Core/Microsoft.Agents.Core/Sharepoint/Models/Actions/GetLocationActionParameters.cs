﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint parameters for a Get Location action.
    /// </summary>
    public class GetLocationActionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetLocationActionParameters"/> class.
        /// </summary>
        public GetLocationActionParameters()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets a value indicating whether the location on the map can be chosen of type <see cref="bool"/>. 
        /// </summary>
        /// <value>This value indicates whether a location on the map can be chosen.</value>
        public bool ChooseLocationOnMap { get; set; }
    }
}
