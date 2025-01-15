// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core.SharePoint.Models.Actions
{
    /// <summary>
    /// Action.Submit.
    /// </summary>
    public class SubmitAction : BaseAction, IAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubmitAction"/> class.
        /// </summary>
        public SubmitAction()
            : base("Submit")
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="Dictionary{TKey, TValue}"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        #pragma warning disable CA2227
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Gets or Sets confirmation dialog associated with this action of type <see cref="ConfirmationDialog"/>.
        /// </summary>
        /// <value>This value is the confirmation dialog associated with this action.</value>
        public ConfirmationDialog ConfirmationDialog { get; set; }
    }
}
