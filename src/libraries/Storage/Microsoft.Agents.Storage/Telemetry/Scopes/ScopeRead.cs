namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    public class ScopeRead : ScopeStorageOperation
    {
        public ScopeRead(int keyCount) : base(Constants.ScopeRead, Constants.OperationRead, keyCount)
        {
        }
    }
}
