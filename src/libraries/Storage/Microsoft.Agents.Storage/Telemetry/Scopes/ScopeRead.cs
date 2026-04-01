namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    internal class ScopeRead : ScopeStorageOperation
    {
        public ScopeRead(int keyCount) : base(Constants.ScopeRead, Constants.OperationRead, keyCount)
        {
        }
    }
}
