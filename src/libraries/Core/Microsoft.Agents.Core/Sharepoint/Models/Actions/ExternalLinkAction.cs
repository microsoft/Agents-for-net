﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint external link action.
    /// </summary>
    public class ExternalLinkAction : BaseAction, IAction, IOnCardSelectionAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalLinkAction"/> class.
        /// </summary>
        public ExternalLinkAction()
            : base("ExternalLink")
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="ExternalLinkActionParameters"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        public ExternalLinkActionParameters Parameters { get; set; }
    }
}
