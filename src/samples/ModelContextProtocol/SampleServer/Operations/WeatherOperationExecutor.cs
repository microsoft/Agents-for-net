using System.ComponentModel;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Sampling;
using Microsoft.Agents.Core.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Microsoft.Agents.Mcp.Server.Sample.Operations;

public struct WeatherInput
{
    [Description("the City")]
    public required string Location { get; init; }

}

public struct WeatherOutput
{
    public required string Temperature { get; init; }
    public required string Status { get; init; }
    public required string Type { get; init; }
}

public class WeatherOperationExecutor : McpToolExecutorBase<WeatherInput, WeatherOutput>
{
    public override string Id => "get_Weather";

    public override string Description => "Gets the weather for a location";

    public override async Task<WeatherOutput> ExecuteAsync(McpRequest<WeatherInput> payload, IMcpContext context, CancellationToken ct)
    {
        var result = await context.PostRequestAsync(new McpSamplingRequest(new SamplingParameters()
        {
            SystemPrompt = "Leveraging existing context or by asking, figure out if they want to use Celcius or Fahrenheit",
        }), ct);

        await context.PostNotificationAsync(new McpLogNotification<ActivityData>(
            new NotificationParameters<ActivityData>()
            {
                Level = "notice",
                Logger = "echo",
                Data = new ActivityData()
                {
                    Type = "activity",
                    Content = new Activity()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "message",
                        Text = $"We got user preferences: " + JsonSerializer.Serialize(result),
                    }
                }
            }), ct);

        return new WeatherOutput { Temperature = "16", Status = "Cloudy, 0 rain", Type = "Celcius" };
    }
}