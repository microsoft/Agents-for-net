// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Response schema sent back from Azure Bot Service Token Service required to initiate a user token direct post.
    /// </summary>
    public class TokenPostResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenPostResource"/> class.
        /// </summary>
        public TokenPostResource()
        {
        }

        /// <summary>
        /// Gets or sets the shared access signature url used to directly post a token to Azure Bot Service Token Service.
        /// </summary>
        /// <value>The URI.</value>
#pragma warning disable CA1056 // Uri properties should not be strings
        public string SasUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings
    }
}
