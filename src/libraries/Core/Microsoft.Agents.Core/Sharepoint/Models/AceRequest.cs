﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models
{
    /// <summary>
    /// ACE invoke request payload.
    /// </summary>
    public class AceRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AceRequest"/> class.
        /// </summary>
        public AceRequest()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AceRequest"/> class.
        /// </summary>
        /// <param name="data">ACE request data.</param>
        /// <param name="properties">ACE properties data.</param>
        public AceRequest(object data = default, object properties = default)
        { 
            Data = data;
            Properties = properties;
        }

        /// <summary>
        /// Gets or sets user ACE request data.
        /// </summary>
        /// <value>The ACE request data.</value>
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets ACE properties data. Free payload with key-value pairs.
        /// </summary>
        /// <value>ACE Properties object.</value>
        public object Properties { get; set; }
    }
}
