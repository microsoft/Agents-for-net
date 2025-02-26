// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Envelope for cards for a <see cref="TabResponse"/>.
    /// </summary>
    public class TabResponseCards
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabResponseCards"/> class.
        /// </summary>
        public TabResponseCards()
        {
        }

        /// <summary>
        /// Gets or sets adaptive cards for this card tab response.
        /// </summary>
        /// <value>
        /// Cards for this <see cref="TabResponse"/>.
        /// </value>
#pragma warning disable CA2227 // Collection properties should be read only
        public IList<TabResponseCard> Cards { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
