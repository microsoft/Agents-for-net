namespace Microsoft.Agents.Storage.Telemetry
{
    internal static class Constants
    {
        internal static readonly string ScopeRead = "agents.storage.read";
        internal static readonly string ScopeWrite = "agents.storage.write";
        internal static readonly string ScopeDelete = "agents.storage.delete";

        internal static readonly string MetricOperationTotal = "agents.storage.operation.total";
        internal static readonly string MetricOperationDuration = "agents.storage.operation.duration";

        internal static readonly string OperationRead = "read";
        internal static readonly string OperationWrite = "write";
        internal static readonly string OperationDelete = "delete";
    }
}
