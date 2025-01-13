// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Client
{
    public interface IChannelInfo
    {
        /// <summary>
        /// Gets or sets Alias of the channel.
        /// </summary>
        /// <value>
        /// Alias of the channel.
        /// </value>
        public string Alias { get; set; }

        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the client factory name for the channel.
        /// </summary>
        public string ChannelFactory { get; set; }

        public IDictionary<string, string> ConnectionSettings { get; set; }
    }
}
