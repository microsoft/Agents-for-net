﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint select media action.
    /// </summary>
    public class SelectMediaAction : BaseAction, IAction, IOnCardSelectionAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SelectMediaAction"/> class.
        /// </summary>
        public SelectMediaAction()
            : base("VivaAction.SelectMedia")
        {
            // Do nothing
        }
        
        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="SelectMediaActionParameters"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        public SelectMediaActionParameters Parameters { get; set; }
    }
}
