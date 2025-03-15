// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// A IConfiguration based IConnections.
    /// </summary>
    /// <remarks>
    /// "Connections": {
    ///   "BotServiceConnection": {
    ///   "Assembly": "Microsoft.Agents.Authentication.Msal",
    ///   "Type": "Microsoft.Agents.Authentication.Msal.MsalAuth",
    ///   "Settings": {
    ///   }
    /// },
    /// "ConnectionsMap": [
    ///  { 
    ///    "ServiceUrl": "*",
    ///    "Connection": "BotServiceConnection"
    /// }
    /// 
    /// The type indicated must have the constructor: (IServiceProvider systemServiceProvider, IConfigurationSection configurationSection).
    /// The 'configurationSection' argument is the 'Settings' portion of the connection.
    /// 
    /// If 'ConnectionsMap' is not specified, the first Connection is used as the default.
    /// </remarks>
    public class ConfigurationConnections : IConnections
    {
        private readonly Dictionary<string, ConnectionDefinition> _connections;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<ConnectionMapItem> _map;
        private readonly ILogger<ConfigurationConnections> _logger;

        public ConfigurationConnections(IServiceProvider systemServiceProvider, IConfiguration configuration, string connectionsKey = "Connections", string mapKey = "ConnectionsMap")
        {
            ArgumentNullException.ThrowIfNullOrEmpty(connectionsKey);
            ArgumentNullException.ThrowIfNullOrEmpty(mapKey);

            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _logger = (ILogger<ConfigurationConnections>)systemServiceProvider.GetService(typeof(ILogger<ConfigurationConnections>));

            _map = configuration
                .GetSection(mapKey)
                .Get<List<ConnectionMapItem>>() ?? Enumerable.Empty<ConnectionMapItem>();

            if (!_map.Any())
            {
                _logger.LogWarning("No connections map found in configuration.");
            }

            _connections = configuration
                .GetSection(connectionsKey)
                .Get<Dictionary<string, ConnectionDefinition>>() ?? [];
            if (!_connections.Any())
            {
                _logger.LogWarning("No connections found in configuration.");
            }

            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, _logger);

            foreach (var connection in _connections)
            {
                connection.Value.Constructor = assemblyLoader.GetProviderConstructor(connection.Key, connection.Value.Assembly, connection.Value.Type);
            }
        }

        /// <inheritdoc/>
        public IAccessTokenProvider GetConnection(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            return GetConnectionInstance(name);
        }

        public bool TryGetConnection(string name, out IAccessTokenProvider connection)
        {
            if (!_connections.TryGetValue(name, out ConnectionDefinition definition))
            {
                connection = null;
                return false;
            }

            connection = GetConnectionInstance(definition, doThrow: false);
            return connection != null;
        }

        /// <inheritdoc/>
        public IAccessTokenProvider GetDefaultConnection()
        {
            // if no connections, abort and return null.
            if (!_connections.Any())
            {
                _logger.LogError(ErrorHelper.MissingAuthenticationConfiguration.description);
                throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
                {
                    HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
                    HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
                };
            }

            // Return the wildcard map item instance.
            foreach (var mapItem in _map)
            {
                if (mapItem.ServiceUrl == "*" && string.IsNullOrEmpty(mapItem.Audience))
                {
                    return GetConnectionInstance(mapItem.Connection);
                }
            }

            // Otherwise, return the first connection.
            return GetConnectionInstance(_connections.FirstOrDefault().Value);
        }

        /// <summary>
        /// Finds a connection based on a map.
        /// </summary>
        /// <remarks>
        /// "ConnectionsMap":
        /// [
        ///    {
        ///       "ServiceUrl": "http://*..botframework.com/*.",
        ///       "Audience": optional,
        ///       "Connection": "BotServiceConnection"
        ///    }
        /// ]
        /// 
        /// ServiceUrl is:  A regex to match with, or "*" for any serviceUrl value.
        /// Connection is: A name in the 'Connections'.
        /// </remarks>        
        /// <param name="claimsIdentity"></param>
        /// <param name="serviceUrl"></param>
        /// <returns></returns>
        public IAccessTokenProvider GetTokenProvider(ClaimsIdentity claimsIdentity, string serviceUrl)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            ArgumentNullException.ThrowIfNullOrEmpty(serviceUrl);

            if (!_map.Any())
            {
                return GetDefaultConnection();
            }

            var audience = BotClaims.GetAppId(claimsIdentity);

            // Find a match, in document order.
            foreach (var mapItem in _map)
            {
                var audienceMatch = true;
                if (!string.IsNullOrEmpty(mapItem.Audience))
                {
                    audienceMatch = mapItem.Audience.Equals(audience, StringComparison.OrdinalIgnoreCase);
                }

                if (audienceMatch)
                {
                    if (mapItem.ServiceUrl == "*" || string.IsNullOrEmpty(mapItem.ServiceUrl))
                    {
                        return GetConnectionInstance(mapItem.Connection);
                    }

                    var match = Regex.Match(serviceUrl, mapItem.ServiceUrl, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return GetConnectionInstance(mapItem.Connection);
                    }
                }
            }

            return null;
        }

        private IAccessTokenProvider GetConnectionInstance(string name)
        {
            if (!_connections.TryGetValue(name, out ConnectionDefinition value))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.ConnectionNotFoundByName, null, name);
            }
            return GetConnectionInstance(value);
        }

        private IAccessTokenProvider GetConnectionInstance(ConnectionDefinition connection, bool doThrow = true)
        {
            if (connection.Instance != null)
            {
                // Return existing instance.
                return connection.Instance;
            }

            try
            {
                // Construct the provider
                connection.Instance = connection.Constructor.Invoke([_serviceProvider, connection.Settings]) as IAccessTokenProvider;
                return connection.Instance;
            }
            catch (Exception ex)
            {
                if (doThrow)
                {
                    throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateAuthModuleProvider, ex, connection.Type);
                }
            }
            return null;
        }
    }
}
