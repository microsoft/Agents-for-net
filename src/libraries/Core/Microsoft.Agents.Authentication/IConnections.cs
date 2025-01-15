// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System.Security.Claims;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Provides access to the token access connections.
    /// </summary>
    public interface IConnections
    {
        /// <summary>
        /// Gets a connection by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>IAccessTokenProvider</returns>
        IAccessTokenProvider GetConnection(string name);

        /// <summary>
        /// Gets the default connection.
        /// </summary>
        /// <returns>IAccessTokenProvider</returns>
        IAccessTokenProvider GetDefaultConnection();

        /// <summary>
        /// Finds a connection based on a map.
        /// </summary>
        /// <remarks>
        /// An Activity.ServiceUrl value.
        /// </remarks>        
        /// <param name="claimsIdentity"></param>
        /// <param name="serviceUrl"></param>
        /// <returns></returns>
        IAccessTokenProvider GetTokenProvider(ClaimsIdentity claimsIdentity, string serviceUrl);
    }
}
