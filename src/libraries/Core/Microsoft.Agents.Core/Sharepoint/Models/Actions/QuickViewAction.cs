﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint Quick View action.
    /// </summary>
    public class QuickViewAction : BaseAction, IAction, IOnCardSelectionAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuickViewAction"/> class.
        /// </summary>
        public QuickViewAction()
            : base("QuickView")
        {
            // Do nothing
        }
        
        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="QuickViewActionParameters"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        public QuickViewActionParameters Parameters { get; set; }
    }
}
