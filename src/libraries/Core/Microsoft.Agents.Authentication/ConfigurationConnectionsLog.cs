// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Agents.Authentication
{
    // Event ID registry for ConfigurationConnections log messages:
    //   1 = Connections config block (LogConnectionsConfig)
    //   2 = ConnectionsMap block (LogConnectionsMap)
    //   Future messages: start from 3
    public partial class ConfigurationConnections
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Connections:\n{Connections}")]
        private static partial void LogConnectionsConfig(ILogger logger, string connections);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "ConnectionsMap:\n{Map}")]
        private static partial void LogConnectionsMap(ILogger logger, string map);
#else
        private static void LogConnectionsConfig(ILogger logger, string connections)
        {
            logger.LogDebug("Connections:\n{Connections}", connections);
        }

        private static void LogConnectionsMap(ILogger logger, string map)
        {
            logger.LogDebug("ConnectionsMap:\n{Map}", map);
        }
#endif

        private void LogConnections()
        {
            LogConnectionsConfig(_logger, FormatConnectionsBlock());
            LogConnectionsMap(_logger, FormatMapBlock());
        }

        private string FormatConnectionsBlock()
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var connection in _connections)
            {
                if (!first)
                {
                    sb.AppendLine();
                }
                first = false;

                var def = connection.Value;
                var type = def.Instance != null ? def.Instance.GetType().Name : def.Type ?? "(default)";
                sb.AppendLine($"  {connection.Key} ({type}):");

                if (def.Settings != null)
                {
                    foreach (var child in def.Settings.GetChildren())
                    {
                        sb.AppendLine($"    {child.Key}={FormatSettingValue(child)}");
                    }
                }
                else
                {
                    sb.AppendLine("    (no settings)");
                }
            }

            TrimTrailingNewline(sb);
            return sb.ToString();
        }

        private string FormatMapBlock()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _map.Count; i++)
            {
                var item = _map[i];
                sb.Append($"  [{i}] ServiceUrl={item.ServiceUrl} Connection={item.Connection}");
                if (!string.IsNullOrEmpty(item.Audience))
                {
                    sb.Append($" Audience={item.Audience}");
                }
                if (i < _map.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
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

        private static void TrimTrailingNewline(StringBuilder sb)
        {
            while (sb.Length > 0 && (sb[sb.Length - 1] == '\n' || sb[sb.Length - 1] == '\r'))
            {
                sb.Length--;
            }
        }
    }
}
