namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    public class ScopeDelete : ScopeStorageOperation
    {
        public ScopeDelete(int keyCount) : base(Constants.ScopeDelete, Constants.OperationDelete, keyCount)
        {
        }
    }
}
