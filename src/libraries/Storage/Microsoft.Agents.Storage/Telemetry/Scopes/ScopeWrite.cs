namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    public class ScopeWrite : ScopeStorageOperation
    {
        public ScopeWrite(int keyCount) : base(Constants.ScopeWrite, Constants.OperationWrite, keyCount)
        {
        }
    }
}
