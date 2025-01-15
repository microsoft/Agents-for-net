﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane label properties object.
    /// </summary>
    public class PropertyPaneLabelProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyPaneLabelProperties"/> class.
        /// </summary>
        public PropertyPaneLabelProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the display text for the label of type <see cref="string"/>.
        /// </summary>
        /// <value>This value is the text of the property pane label.</value>
        public string Text { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the associated form field is required or not. of type <see cref="bool"/>.
        /// </summary>
        /// <value>This value indicates whether the property pane field is required.</value>
        public bool Required { get; set; }
    }
}
