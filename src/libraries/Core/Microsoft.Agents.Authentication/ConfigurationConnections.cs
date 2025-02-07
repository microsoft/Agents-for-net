// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Errors;
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

            if ( !_map.Any() )
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

            var assemblyLoader = new AssemblyLoader(AssemblyLoadContext.Default);

            foreach (var connection in _connections)
            {
                connection.Value.Constructor = assemblyLoader.GetProviderConstructor(connection.Key, connection.Value.Assembly, connection.Value.Type);
            }
        }

        /// <inheritdoc/>
        public IAccessTokenProvider GetConnection(string name)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(name);

            return GetConnectionInstance(name);
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
                return null;
            }

            return GetConnectionInstance(value);
        }

        private IAccessTokenProvider GetConnectionInstance(ConnectionDefinition connection)
        {
            if (connection.Instance != null)
            {
                // Return existing instance.
                return connection.Instance;
            }

            // Construct the provider
            connection.Instance = connection.Constructor.Invoke([_serviceProvider, connection.Settings]) as IAccessTokenProvider;
            return connection.Instance;
        }
    }

    class ConnectionDefinition
    {
        public string Assembly { get; set; }
        public string Type { get; set; }
        public IConfigurationSection Settings { get; set; }
        public ConstructorInfo Constructor { get; set; }
        public IAccessTokenProvider Instance { get; set; }
    }

    class ConnectionMapItem
    {
        public string ServiceUrl { get; set; }
        public string Audience { get; set; }
        public string Connection { get; set; }
    }

    class AssemblyLoader(AssemblyLoadContext loadContext)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));

        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(name);

            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentNullException(nameof(assemblyName), $"Assembly for '{name}' is missing or empty");
            }
            
            if (string.IsNullOrEmpty(typeName))
            {
                // A Type name wasn't given in config.  Just get the first matching valid type.
                // This is only really appropriate if an assembly only has a single IAccessTokenProvider.
                return GetProviderConstructors(assemblyName).First();
            }

            // This throws for invalid assembly name.
            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));

            Type type = assembly.GetType(typeName);
            if (!IsValidProviderType(type))
            {
                // Perhaps config left off the full type name?
                type = assembly.GetType($"{assemblyName}.{typeName}");
                if (!IsValidProviderType(type))
                {
                    throw new InvalidOperationException($"Type '{typeName}' not found in Assembly '{assemblyName}' or is the wrong type for '{name}'");
                }
            }

            return GetConstructor(type) ?? throw new InvalidOperationException($"Type '{typeName},{assemblyName}' does not have the required constructor.");
        }

        public IEnumerable<ConstructorInfo> GetProviderConstructors(string assemblyName)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(assemblyName);

            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));

            foreach (Type loadedType in assembly.GetTypes())
            {
                if (!IsValidProviderType(loadedType))
                {
                    continue;
                }

                ConstructorInfo constructor = GetConstructor(loadedType);
                if (constructor == null)
                {
                    continue;
                }

                yield return constructor;
            }
        }

        private static bool IsValidProviderType(Type type)
        {
            if (type == null ||
                !typeof(IAccessTokenProvider).IsAssignableFrom(type) ||
                !type.IsPublic ||
                type.IsNested ||
                type.IsAbstract)
            {
                return false;
            }

            return true;
        }

        private static ConstructorInfo GetConstructor(Type type)
        {
            return type.GetConstructor(
                bindingAttr: BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(IServiceProvider), typeof(IConfigurationSection)],
                modifiers: null);
        }
    }
}
