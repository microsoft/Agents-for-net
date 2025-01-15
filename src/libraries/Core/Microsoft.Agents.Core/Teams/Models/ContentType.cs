﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// Defines content type. Depending on contentType, content field will have a different structure. 
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// Content type is Unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Content type is Task.
        /// </summary>
        Task
    }
}
