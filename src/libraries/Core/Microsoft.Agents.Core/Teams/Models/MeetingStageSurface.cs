﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// Specifies meeting stage surface.
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>.
    public class MeetingStageSurface<T> : Surface
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeetingStageSurface{T}"/> class.
        /// </summary>
        public MeetingStageSurface()
            : base(SurfaceType.MeetingStage)
        {
        }

        /// <summary>
        /// Gets or sets the content type of this <see cref="MeetingStageSurface{T}"/>.
        /// </summary>
        /// <value>
        /// The content type of this <see cref="MeetingStageSurface{T}"/>.
        /// </value>
        public ContentType ContentType { get; set; } = ContentType.Task;

        /// <summary>
        /// Gets or sets the content for this <see cref="MeetingStageSurface{T}"/>.
        /// </summary>
        /// <value>
        /// The content of this <see cref="MeetingStageSurface{T}"/>.
        /// </value>
        public T Content { get; set; }
    }
}
