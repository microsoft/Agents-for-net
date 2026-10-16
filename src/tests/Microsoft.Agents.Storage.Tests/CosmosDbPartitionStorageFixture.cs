using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class CosmosDbPartitionStorageFixture : IAsyncLifetime
    {
        private CosmosClient _client;
        private Database _database;

        public string DatabaseId { get; } = $"{CosmosDbConstants.CosmosDatabaseName}-{Guid.NewGuid():N}";

        public async Task InitializeAsync()
        {
            if (IgnoreOnNoEmulatorFact.HasEmulator.Value)
            {
                _client = new CosmosClient(
                    CosmosDbConstants.CosmosServiceEndpoint,
                    CosmosDbConstants.CosmosAuthKey,
                    new CosmosClientOptions());

                var response = await _client.CreateDatabaseAsync(DatabaseId);
                _database = response.Database;
            }
        }

        public async Task DisposeAsync()
        {
            try
            {
                if (_database != null)
                {
                    await _database.DeleteAsync();
                }
            }
            finally
            {
                _client?.Dispose();
            }
        }
    }
}
