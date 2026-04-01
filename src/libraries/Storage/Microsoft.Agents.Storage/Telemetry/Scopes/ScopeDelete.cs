namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    internal class ScopeDelete : ScopeStorageOperation
    {
        public ScopeDelete(int keyCount) : base(Constants.ScopeDelete, Constants.OperationDelete, keyCount)
        {
        }
    }
}
