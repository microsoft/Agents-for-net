﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint show location action.
    /// </summary>
    public class ShowLocationAction : BaseAction, IAction, IOnCardSelectionAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShowLocationAction"/> class.
        /// </summary>
        public ShowLocationAction()
            : base("VivaAction.ShowLocation")
        {
            // Do nothing
        }
        
        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="ShowLocationActionParameters"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        public ShowLocationActionParameters Parameters { get; set; }
    }
}
