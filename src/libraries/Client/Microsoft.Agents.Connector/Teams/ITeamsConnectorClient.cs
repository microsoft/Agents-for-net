// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Teams
{
    /// <summary>
    /// ﻿﻿The Connector for Microsoft Teams allows your bot to perform extended operations on a Microsoft Teams channel.
    /// </summary>
    public interface ITeamsConnectorClient : IConnectorClient
    {
        /// <summary>
        /// Gets the ITeamsOperations.
        /// </summary>
        /// <value>The ITeamsOperations.</value>
        ITeamsOperations Teams { get; }
    }
}
