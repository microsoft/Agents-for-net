// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// Specifies where the notification will be rendered in the meeting UX.
    /// </summary>
    public class Surface
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Surface"/> class.
        /// </summary>
        /// <param name="type">Type of Surface.</param>
        protected Surface(SurfaceType type)
        {
            Type = type;
        }

        /// <summary>
        /// Gets or sets Surface type, the value indicating where the notification will be rendered in the meeting UX.
        /// Note: only one instance of surface type is allowed per request.
        /// </summary>
        /// <value>
        /// The value indicating where the notification will be rendered in the meeting UX.
        /// </value>
        public SurfaceType Type { get; set; }
    }
}
