// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Authentication
{
    // Event ID registry for ConfigurationConnections log messages:
    //   1 = Connections config block (LogConnectionsConfig)
    //   2 = ConnectionsMap block (LogConnectionsMap)
    //   Future messages: start from 3
    public partial class ConfigurationConnections
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Connections: {Connections}")]
        private static partial void LogConnectionsConfig(ILogger logger, string connections);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "ConnectionsMap: {Map}")]
        private static partial void LogConnectionsMap(ILogger logger, string map);
#else
        private static void LogConnectionsConfig(ILogger logger, string connections)
        {
            logger.LogDebug("Connections: {Connections}", connections);
        }

        private static void LogConnectionsMap(ILogger logger, string map)
        {
            logger.LogDebug("ConnectionsMap: {Map}", map);
        }
#endif

        private void LogConnections()
        {
            LogConnectionsConfig(_logger, FormatConnectionsJson());
            LogConnectionsMap(_logger, FormatMapJson());
        }

        private string FormatConnectionsJson()
        {
            var entries = new Dictionary<string, object>();
            foreach (var connection in _connections)
            {
                var def = connection.Value;
                var props = new Dictionary<string, string>();

                var type = def.Instance != null ? def.Instance.GetType().Name : def.Type ?? "MsalAuth";
                props["Type"] = type;

                if (def.Settings != null)
                {
                    foreach (var child in def.Settings.GetChildren())
                    {
                        props[child.Key] = FormatSettingValue(child);
                    }
                }

                entries[connection.Key] = props;
            }

            return JsonSerializer.Serialize(entries);
        }

        private string FormatMapJson()
        {
            var items = new List<Dictionary<string, string>>();
            foreach (var item in _map)
            {
                var entry = new Dictionary<string, string>
                {
                    ["ServiceUrl"] = item.ServiceUrl,
                    ["Connection"] = item.Connection
                };
                if (!string.IsNullOrEmpty(item.Audience))
                {
                    entry["Audience"] = item.Audience;
                }
                items.Add(entry);
            }

            return JsonSerializer.Serialize(items);
        }

        private static string FormatSettingValue(IConfigurationSection section)
        {
            if (string.Equals(section.Key, "ClientSecret", StringComparison.OrdinalIgnoreCase))
            {
                return "[redacted]";
            }

            if (section.Value != null)
            {
                return section.Value;
            }

            // Array or nested section — collect child values
            var values = new List<string>();
            foreach (var child in section.GetChildren())
            {
                values.Add(child.Value ?? "[section]");
            }
            return values.Count > 0 ? $"[{string.Join(", ", values)}]" : "(empty)";
        }
    }
}
