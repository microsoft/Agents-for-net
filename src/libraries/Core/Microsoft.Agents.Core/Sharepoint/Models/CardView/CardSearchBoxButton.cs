﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.SharePoint.Models.Actions;

namespace Microsoft.Agents.Core.SharePoint.Models.CardView
{
    /// <summary>
    /// Card Search box button.
    /// </summary>
    public class CardSearchBoxButton : ICardButtonBase
    {
        /// <summary>
        /// Gets or sets the button's action.
        /// </summary>
        /// <value>Button's action.</value>
        public IAction Action { get; set; }

        /// <summary>
        /// Gets or sets unique Id of the button.
        /// </summary>
        /// <value>Unique Id of the button.</value>
        public string Id { get; set; }
    }
}
