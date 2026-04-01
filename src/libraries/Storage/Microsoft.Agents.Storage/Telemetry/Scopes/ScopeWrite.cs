namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    internal class ScopeWrite : ScopeStorageOperation
    {
        public ScopeWrite(int keyCount) : base(Constants.ScopeWrite, Constants.OperationWrite, keyCount)
        {
        }
    }
}
