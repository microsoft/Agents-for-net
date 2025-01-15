﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.SharePoint.Models
{
    /// <summary>
    /// Adaptive Card Extension Client-side action response to render card view.
    /// </summary>
    public class CardViewHandleActionResponse : BaseHandleActionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CardViewHandleActionResponse"/> class.
        /// </summary>
        public CardViewHandleActionResponse()
        : base(ViewResponseType.Card)
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or sets card view render arguments.
        /// </summary>
        /// <value>Card view render arguments.</value>
        public new CardViewResponse RenderArguments { get; set; }
    }
}
