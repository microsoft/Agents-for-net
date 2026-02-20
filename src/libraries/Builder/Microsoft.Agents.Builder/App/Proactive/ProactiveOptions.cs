// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides configuration options for proactive features, including storage settings.
    /// </summary>
    public class ProactiveOptions
    {
        /// <summary>
        /// Gets or sets the storage provider used for data persistence operations.
        /// </summary>
        public IStorage Storage { get; set; }
    }
}
