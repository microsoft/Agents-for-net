// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines the query options in the <see cref="SearchInvokeValue"/> for Invoke activity with Name of 'application/search'.
    /// </summary>
    public class SearchInvokeOptions
    {
        /// <summary>
        /// Gets or sets the the starting reference number from which ordered search results should be returned.
        /// </summary>
        /// <value>
        /// The the starting reference number from which ordered search results should be returned.
        /// </value>
        public int Skip { get; set; }

        /// <summary>
        /// Gets or sets the number of search results that should be returned.
        /// </summary>
        /// <value>
        /// The number of search results that should be returned.
        /// </value>
        public int Top { get; set; }
    }
}
