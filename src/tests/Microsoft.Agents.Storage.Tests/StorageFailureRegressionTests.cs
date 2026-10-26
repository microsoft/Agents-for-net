// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage;
using Microsoft.Agents.Storage.Blobs;
using Microsoft.Agents.Storage.CosmosDb;
using Microsoft.Azure.Cosmos;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class StorageFailureRegressionTests
    {
        [Theory]
        [InlineData("memory")]
        [InlineData("blobs")]
        [InlineData("cosmos")]
        [InlineData("adapter")]
        public async Task V2_NullValueRejectsWholeBatchBeforeAccessingStorage(string provider)
        {
            var blobs = new Mock<BlobContainerClient>(MockBehavior.Strict);
            var cosmos = new Mock<CosmosClient>(MockBehavior.Strict);
            var legacy = new Mock<IStorage>(MockBehavior.Strict);
            IStorageV2 storage = provider switch
            {
                "memory" => new MemoryStorage(),
                "blobs" => new BlobsStorage(blobs.Object),
                "cosmos" => new CosmosDbPartitionedStorage(cosmos.Object, new CosmosDbPartitionedStorageOptions
                {
                    DatabaseId = "database",
                    ContainerId = "container"
                }),
                _ => StorageCompatibility.AsV2(legacy.Object)
            };
            IReadOnlyDictionary<string, StoreItem> changes = new Dictionary<string, StoreItem>
            {
                ["valid-first"] = new StoreItem { Topic = "valid" },
                ["null-second"] = null
            };

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.WriteAsync(changes, new StorageWriteOptions()));

            Assert.Equal("changes", exception.ParamName);
            Assert.Contains("null-second", exception.Message);
            blobs.VerifyNoOtherCalls();
            cosmos.VerifyNoOtherCalls();
            legacy.VerifyNoOtherCalls();
            if (provider == "memory")
            {
                var read = await storage.ReadAsync((IReadOnlyList<string>)new[] { "valid-first", "null-second" });
                Assert.All(read.Values, result => Assert.Equal(StorageOperationStatus.NotFound, result.Status));
            }
        }

        [Theory]
        [InlineData(StorageWriteMode.Upsert)]
        [InlineData(StorageWriteMode.CreateOnly)]
        [InlineData(StorageWriteMode.Replace)]
        public async Task V2_NullValueDoesNotReplaceExistingMemoryData(StorageWriteMode mode)
        {
            IStorageV2 storage = new MemoryStorage();
            await storage.WriteAsync(new Dictionary<string, StoreItem>
            {
                ["existing"] = new StoreItem { Topic = "original" }
            }, new StorageWriteOptions());

            await Assert.ThrowsAsync<ArgumentException>(() => storage.WriteAsync(
                new Dictionary<string, StoreItem> { ["existing"] = null }, new StorageWriteOptions { Mode = mode }));

            var read = await ((IStorage)storage).ReadAsync<StoreItem>(new[] { "existing" });
            Assert.Equal("original", read["existing"].Topic);
        }

        [Fact]
        public async Task CosmosStorage_ConcurrentCallsWaitForContainerValidation()
        {
            var container = new Mock<Container>();
            using var storage = ExistingCosmosStorage(container);
            var validation = new TaskCompletionSource<ContainerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            int validationCalls = 0;
            container.Setup(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .Callback(() => Interlocked.Increment(ref validationCalls))
                .Returns(validation.Task);
            container.Setup(value => value.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StoredItemResponse(new CosmosDbPartitionedStorage.DocumentStoreItem
                {
                    Id = "k",
                    RealId = "k",
                    Document = JsonNode.Parse("{\"name\":\"original\"}")!.AsObject()
                }));
            IStorage legacy = storage;

            Task first = legacy.ReadAsync(new[] { "k" });
            Task second = legacy.ReadAsync(new[] { "k" });
            bool completedEarly = second.IsCompleted;
            int callsBeforeValidation = validationCalls;
            var response = new Mock<ContainerResponse>();
            response.SetupGet(value => value.Resource).Returns(new ContainerProperties("container", "/id"));
            validation.SetResult(response.Object);
            await Task.WhenAll(first, second);

            Assert.False(completedEarly);
            Assert.Equal(1, callsBeforeValidation);
            Assert.Equal(1, validationCalls);
        }

        [Fact]
        public async Task CosmosStorage_InvalidPartitionKeyIsRejectedOnEveryAttempt()
        {
            var container = new Mock<Container>();
            using var storage = ExistingCosmosStorage(container);
            var response = new Mock<ContainerResponse>();
            response.SetupGet(value => value.Resource).Returns(new ContainerProperties("container", "/unsupported"));
            container.Setup(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => ((IStorage)storage).ReadAsync(new[] { "k" }));
            }
        }

        [Fact]
        public async Task CosmosStorage_TransientContainerReadDoesNotMarkInitializationComplete()
        {
            var container = new Mock<Container>();
            using var storage = ExistingCosmosStorage(container);
            container.Setup(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Container unavailable", HttpStatusCode.ServiceUnavailable, 0, "test", 0));

            for (int attempt = 0; attempt < 2; attempt++)
            {
                CosmosException exception = await Assert.ThrowsAsync<CosmosException>(() => ((IStorage)storage).ReadAsync(new[] { "k" }));
                Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
            }
            container.Verify(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData("BlobNotFound", false)]
        [InlineData("BlobNotFound", true)]
        [InlineData("ContainerNotFound", false)]
        [InlineData("ContainerNotFound", true)]
        public async Task BlobsStorage_DeleteDistinguishesItemAndContainerDisappearing(string errorCode, bool useV2)
        {
            var container = new Mock<BlobContainerClient>();
            var blob = new Mock<BlobClient>();
            container.Setup(value => value.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);
            blob.Setup(value => value.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(global::Azure.Response.FromValue(
                    BlobsModelFactory.BlobProperties(eTag: new ETag("existing-version")), Mock.Of<global::Azure.Response>()));
            blob.Setup(value => value.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(global::Azure.Response.FromValue(false, Mock.Of<global::Azure.Response>()));
            blob.Setup(value => value.DeleteAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "Resource disappeared during deletion", errorCode, null));
            IStorageV2 storage = new BlobsStorage(container.Object);

            async Task DeleteAsync()
            {
                if (useV2)
                {
                    var result = await storage.DeleteAsync((IReadOnlyList<string>)new[] { "k" });
                    Assert.Equal(StorageOperationStatus.NotFound, result["k"].Status);
                }
                else
                {
                    await ((IStorage)storage).DeleteAsync(new[] { "k" });
                }
            }

            if (errorCode == "ContainerNotFound")
            {
                await Assert.ThrowsAsync<RequestFailedException>(DeleteAsync);
            }
            else
            {
                await DeleteAsync();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CosmosStorage_LegacyBatchContinuesPastMissingKey(bool delete)
        {
            var container = new Mock<Container>();
            using var storage = ExistingCosmosStorage(container);
            container.Setup(value => value.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    "missing", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Missing item", HttpStatusCode.NotFound, 0, "test", 0));
            container.Setup(value => value.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    "existing", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StoredItemResponse(new CosmosDbPartitionedStorage.DocumentStoreItem
                {
                    Id = "existing",
                    RealId = "existing",
                    Document = JsonNode.Parse("{\"name\":\"existing\"}")!.AsObject()
                }));
            container.Setup(value => value.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    "missing", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Missing item", HttpStatusCode.NotFound, 0, "test", 0));

            IStorage legacy = storage;
            if (delete)
            {
                await legacy.DeleteAsync(new[] { "missing", "existing" });
                container.Verify(value => value.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    "existing", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            else
            {
                Assert.True((await legacy.ReadAsync(new[] { "missing", "existing" })).ContainsKey("existing"));
            }
        }

        [Theory]
        [InlineData("read-v1")]
        [InlineData("read-v2")]
        [InlineData("write-v2")]
        [InlineData("delete-v1")]
        [InlineData("delete-v2")]
        public async Task CosmosStorage_MissingContainerIsNotReportedAsMissingItem(string operation)
        {
            var container = new Mock<Container>();
            using var storage = ExistingCosmosStorage(container);
            var missing = new CosmosException("Missing resource", HttpStatusCode.NotFound, 0, "test", 0);
            container.Setup(value => value.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(missing);
            container.Setup(value => value.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(
                    It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(missing);
            container.Setup(value => value.UpsertItemAsync(It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(),
                    It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(missing);
            // Initialize against a valid container, then simulate its disappearance.
            await ((IStorage)storage).ReadAsync(new[] { "k" });
            container.Setup(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(missing);

            await Assert.ThrowsAsync<CosmosException>(async () =>
            {
                switch (operation)
                {
                    case "read-v1":
                        await ((IStorage)storage).ReadAsync(new[] { "k" });
                        break;
                    case "read-v2":
                        await ((IStorageV2)storage).ReadAsync((IReadOnlyList<string>)new[] { "k" });
                        break;
                    case "delete-v1":
                        await ((IStorage)storage).DeleteAsync(new[] { "k" });
                        break;
                    case "delete-v2":
                        await ((IStorageV2)storage).DeleteAsync((IReadOnlyList<string>)new[] { "k" });
                        break;
                    default:
                        await storage.WriteAsync(new Dictionary<string, object> { ["k"] = new StoreItem() }, new StorageWriteOptions());
                        break;
                }
            });
        }

        private static CosmosDbPartitionedStorage ExistingCosmosStorage(Mock<Container> container)
        {
            var client = new Mock<CosmosClient>();
            var response = new Mock<ContainerResponse>();
            response.SetupGet(value => value.Resource).Returns(new ContainerProperties("container", "/id"));
            container.Setup(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);
            client.Setup(value => value.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(container.Object);
            return new CosmosDbPartitionedStorage(client.Object, new CosmosDbPartitionedStorageOptions
            {
                DatabaseId = "database",
                ContainerId = "container"
            });
        }

        private sealed class StoredItemResponse(CosmosDbPartitionedStorage.DocumentStoreItem resource)
            : ItemResponse<CosmosDbPartitionedStorage.DocumentStoreItem>
        {
            public override CosmosDbPartitionedStorage.DocumentStoreItem Resource => resource;
        }

        [Theory]
        [InlineData("read-v1")]
        [InlineData("read-v2")]
        [InlineData("write-v2")]
        [InlineData("replace-v2")]
        [InlineData("delete-v2")]
        public async Task BlobsStorage_MissingContainerIsNotReportedAsMissingItem(string operation)
        {
            var container = new Mock<BlobContainerClient>();
            var blob = new Mock<BlobClient>();
            container.Setup(value => value.GetBlobClient(It.IsAny<string>())).Returns(blob.Object);
            var missingContainer = new RequestFailedException(404, "Missing container", "ContainerNotFound", null);
            blob.Setup(value => value.DownloadAsync(It.IsAny<CancellationToken>())).ThrowsAsync(missingContainer);
            blob.Setup(value => value.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(missingContainer);
            IStorageV2 storage = new BlobsStorage(container.Object);

            await Assert.ThrowsAsync<RequestFailedException>(async () =>
            {
                switch (operation)
                {
                    case "read-v1":
                        await ((IStorage)storage).ReadAsync(new[] { "k" });
                        break;
                    case "read-v2":
                        await storage.ReadAsync((IReadOnlyList<string>)new[] { "k" });
                        break;
                    case "delete-v2":
                        await storage.DeleteAsync((IReadOnlyList<string>)new[] { "k" });
                        break;
                    default:
                        await storage.WriteAsync(new Dictionary<string, object> { ["k"] = new StoreItem() },
                            new StorageWriteOptions { Mode = operation == "replace-v2" ? StorageWriteMode.Replace : StorageWriteMode.Upsert });
                        break;
                }
            });
        }

        [Fact]
        public async Task V2_NoOptionsWritePreservesLegacyEtagCheck()
        {
            var memory = new MemoryStorage();
            IStorageV2 storageV2 = memory;
            await memory.WriteAsync(new Dictionary<string, object> { ["item"] = new StoreItem { Topic = "initial", ETag = "*" } });
            var stale = (await memory.ReadAsync<StoreItem>(["item"]))["item"];
            await memory.WriteAsync(new Dictionary<string, StoreItem> { ["item"] = new StoreItem { Topic = "newer", ETag = "*" } });
            stale.Topic = "stale";

            await Assert.ThrowsAsync<EtagException>(() =>
                storageV2.WriteAsync(new Dictionary<string, object> { ["item"] = stale }));
            await Assert.ThrowsAsync<EtagException>(() =>
                storageV2.WriteAsync(new Dictionary<string, StoreItem> { ["item"] = stale }));
            Assert.Equal("newer", (await memory.ReadAsync<StoreItem>(["item"]))["item"].Topic);

            var result = await storageV2.WriteAsync(new Dictionary<string, StoreItem> { ["item"] = stale }, new StorageWriteOptions());
            Assert.Equal(StorageOperationStatus.Succeeded, result["item"].Status);
            Assert.Equal("stale", (await memory.ReadAsync<StoreItem>(["item"]))["item"].Topic);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CosmosStorage_FailedCreationIsRetried(bool compatibilityMode)
        {
            var client = new Mock<CosmosClient>();
            var container = new Mock<Container>();
            var database = new Mock<Database> { CallBase = true };
            database.SetupGet(value => value.Client).Returns(client.Object);
            database.SetupGet(value => value.Id).Returns("database");
            var response = new Mock<ContainerResponse>();
            response.SetupGet(value => value.Container).Returns(container.Object);
            client.Setup(value => value.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(container.Object);
            client.Setup(value => value.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(value => value.DefineContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string id, string partitionKey) =>
                    new Microsoft.Azure.Cosmos.Fluent.ContainerBuilder(database.Object, id, partitionKey));
            container.Setup(value => value.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Missing container", HttpStatusCode.NotFound, 0, "test", 0));
            database.SetupSequence(value => value.CreateContainerIfNotExistsAsync(
                    It.IsAny<ContainerProperties>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Failed creation", HttpStatusCode.ServiceUnavailable, 0, "test", 0))
                .ReturnsAsync(response.Object);
            container.Setup(value => value.UpsertItemAsync(
                    It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(), It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Probe reached initialized container", HttpStatusCode.BadRequest, 0, "test", 0));
            using var storage = new CosmosDbPartitionedStorage(client.Object, new CosmosDbPartitionedStorageOptions
            {
                DatabaseId = "database",
                ContainerId = "container",
                CompatibilityMode = compatibilityMode
            });
            var changes = new Dictionary<string, object> { ["k"] = new StoreItem() };

            CosmosException first = await Assert.ThrowsAsync<CosmosException>(() => storage.WriteAsync(changes, new StorageWriteOptions()));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
            CosmosException retried = await Assert.ThrowsAsync<CosmosException>(() => storage.WriteAsync(changes, new StorageWriteOptions()));
            Assert.Equal(HttpStatusCode.BadRequest, retried.StatusCode);
            container.Verify(value => value.UpsertItemAsync(
                It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            database.Verify(value => value.CreateContainerIfNotExistsAsync(
                It.IsAny<ContainerProperties>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MemoryStorage_RawJsonReadReturnsIndependentSnapshot(bool useV2)
        {
            var backing = new Dictionary<string, JsonObject>
            {
                ["k"] = JsonNode.Parse("{\"nested\":{\"name\":\"original\"}}")!.AsObject()
            };
            IStorage storage = new MemoryStorage(dictionary: backing);
            object value = useV2
                ? (await ((IStorageV2)storage).ReadAsync((IReadOnlyList<string>)new[] { "k" }))["k"].Value
                : (await storage.ReadAsync(new[] { "k" }))["k"];

            ((JsonObject)value)["nested"]["name"] = "changed";

            var nextRead = (JsonObject)(await storage.ReadAsync(new[] { "k" }))["k"];
            Assert.Equal("original", nextRead["nested"]["name"].GetValue<string>());
        }

        [Fact]
        public async Task BlobsStorage_FailedInitializationIsRetried()
        {
            var container = new Mock<BlobContainerClient>();
            container.SetupSequence(client => client.CreateIfNotExistsAsync(
                    It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(503, "Initialization failed."))
                .ReturnsAsync((global::Azure.Response<BlobContainerInfo>)null);
            IStorage storage = new BlobsStorage(container.Object);

            await Assert.ThrowsAsync<RequestFailedException>(() => storage.ReadAsync(Array.Empty<string>()));
            await storage.ReadAsync(Array.Empty<string>());

            container.Verify(client => client.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task BlobsStorage_ConcurrentCallsWaitForInitialization()
        {
            var creation = new TaskCompletionSource<global::Azure.Response<BlobContainerInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var container = new Mock<BlobContainerClient>();
            container.Setup(client => client.CreateIfNotExistsAsync(
                    It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
                .Returns(creation.Task);
            IStorage storage = new BlobsStorage(container.Object);

            Task first = storage.ReadAsync(Array.Empty<string>());
            Task second = storage.ReadAsync(Array.Empty<string>());
            bool returnedBeforeCreation = second.IsCompleted;
            creation.SetResult(null);
            await Task.WhenAll(first, second);

            Assert.False(returnedBeforeCreation);
            container.Verify(client => client.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MemoryStorage_FailedReadPreservesDataAndFailsAgain(bool useV2)
        {
            var backing = new Dictionary<string, JsonObject>();
            IStorage storage = new MemoryStorage(dictionary: backing);
            await storage.WriteAsync(new Dictionary<string, object> { ["k"] = new ThrowsOnRead() });
            JsonNode before = backing["k"].DeepClone();

            for (int read = 0; read < 2; read++)
            {
                if (useV2)
                {
                    await Assert.ThrowsAsync<JsonException>(() =>
                        ((IStorageV2)storage).ReadAsync((IReadOnlyList<string>)new[] { "k" }));
                }
                else
                {
                    await Assert.ThrowsAsync<JsonException>(() => storage.ReadAsync(new[] { "k" }));
                }

                await Assert.ThrowsAsync<JsonException>(() => storage.ReadAsync<ThrowsOnRead>(new[] { "k" }));

                Assert.True(JsonNode.DeepEquals(before, backing["k"]));
            }
        }

        public sealed class ThrowsOnRead
        {
            public string Name { get; } = "original";

            public ThrowsOnRead()
            {
            }

            [JsonConstructor]
            public ThrowsOnRead(string name)
            {
                throw new JsonException("Cannot deserialize this item.");
            }
        }
    }
}
