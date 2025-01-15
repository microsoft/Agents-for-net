// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// Tab SuggestedActions (Only when type is 'auth' or 'silentAuth').
    /// </summary>
    public class TabSuggestedActions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabSuggestedActions"/> class.
        /// </summary>
        public TabSuggestedActions()
        {
        }

        /// <summary>
        /// Gets or sets actions for a card tab response.
        /// </summary>
        /// <value>
        /// Actions for this <see cref="TabSuggestedActions"/>.
        /// </value>
#pragma warning disable CA2227 // Collection properties should be read only
        public IList<CardAction> Actions { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
