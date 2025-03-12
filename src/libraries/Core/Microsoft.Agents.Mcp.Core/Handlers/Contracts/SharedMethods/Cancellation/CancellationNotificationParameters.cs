namespace Microsoft.Agents.Mcp.Core.Handlers.Contracts.SharedMethods.Cancellation
{
    public class CancellationNotificationParameters
    {
        public required string RequestId { get; init; }
    
        public string? Reason { get; init; }
    }
}