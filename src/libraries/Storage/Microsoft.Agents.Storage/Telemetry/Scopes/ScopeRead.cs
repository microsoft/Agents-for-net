namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="ScopeStorageOperation"/> that traces a storage read operation.
    /// </summary>
    public class ScopeRead : ScopeStorageOperation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeRead"/> class.
        /// </summary>
        /// <param name="keyCount">The number of keys being read.</param>
        public ScopeRead(int keyCount) : base(Constants.ScopeRead, Constants.OperationRead, keyCount)
        {
        }
    }
}
