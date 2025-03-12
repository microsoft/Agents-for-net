using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;


public class McpLogNotification : McpNotification<NotificationParameters>
{
    public static readonly string MethodName = "notifications/message";

    [SetsRequiredMembers]
    public McpLogNotification(NotificationParameters parameters)
    {
        Method = MethodName;
        Parameters = parameters;
    }
}

public class McpLogNotification<LogData> : McpNotification<NotificationParameters<LogData>>
{
    [SetsRequiredMembers]
    public McpLogNotification(NotificationParameters<LogData> parameters)
    {
        Method = McpLogNotification.MethodName;
        Parameters = parameters;
    }
}


public class NotificationParameters : NotificationParameters<object> 
{
}

public class NotificationParameters<ResultType>
{
    [JsonPropertyName("level")]
    public required string Level { get; set; }

    [JsonPropertyName("logger")]
    public required string Logger { get; set; }

    [JsonPropertyName("data")]
    public required ResultType Data { get; set; }
}