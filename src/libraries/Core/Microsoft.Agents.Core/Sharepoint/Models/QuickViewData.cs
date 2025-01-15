﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models
{
    /// <summary>
    /// SharePoint Quick View Data object.
    /// </summary>
    public class QuickViewData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuickViewData"/> class.
        /// </summary>
        public QuickViewData()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the title of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the title of the quick view data.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or Sets the description of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the description of the quick view data.</value>
        public string Description { get; set; }
    }
}
