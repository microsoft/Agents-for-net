// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Moq;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    /// <summary>
    /// Tests for AgentState thread safety and synchronization.
    /// </summary>
    public class AgentStateThreadSafetyTests
    {
        [Fact]
        public async Task State_ConcurrentReads_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("key1", "value1");
            userState.SetValue("key2", "value2");

            // Act - Multiple threads reading concurrently
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var val1 = userState.GetValue<string>("key1");
                    var val2 = userState.GetValue<string>("key2");
                    Assert.Equal("value1", val1);
                    Assert.Equal("value2", val2);
                }
            }));

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task State_ConcurrentWrites_ShouldNotCorruptState()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act - Multiple threads writing different keys concurrently
            var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            {
                userState.SetValue($"key{i}", $"value{i}");
            }));

            await Task.WhenAll(tasks);

            // Assert - All values should be present
            for (int i = 0; i < 50; i++)
            {
                var value = userState.GetValue<string>($"key{i}");
                Assert.Equal($"value{i}", value);
            }
        }

        [Fact]
        public async Task State_ConcurrentReadWrite_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("counter", 0);

            // Act - Some threads reading, some writing
            var readTasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var value = userState.GetValue<int>("counter");
                    Assert.True(value >= 0);
                }
            }));

            var writeTasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            {
                userState.SetValue("counter", i);
            }));

            // Assert - Should complete without exceptions
            await Task.WhenAll(readTasks.Concat(writeTasks));
        }

        [Fact]
        public async Task State_ConcurrentGetValueWithDefault_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act - Multiple threads calling GetValue with defaultValueFactory
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            {
                var value = userState.GetValue("key", () => "default");
                Assert.Equal("default", value);
            }));

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task State_ConcurrentHasValue_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("key1", "value1");

            // Act - Multiple threads checking HasValue
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var has1 = userState.HasValue("key1");
                    var has2 = userState.HasValue("nonexistent");
                    Assert.True(has1);
                    Assert.False(has2);
                }
            }));

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task State_ConcurrentDeleteValue_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Add multiple keys
            for (int i = 0; i < 50; i++)
            {
                userState.SetValue($"key{i}", $"value{i}");
            }

            // Act - Multiple threads deleting different keys
            var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            {
                userState.DeleteValue($"key{i}");
            }));

            await Task.WhenAll(tasks);

            // Assert - All keys should be deleted
            for (int i = 0; i < 50; i++)
            {
                Assert.False(userState.HasValue($"key{i}"));
            }
        }

        [Fact]
        public async Task State_ConcurrentClearState_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("key", "value");

            // Act - Multiple threads calling ClearState
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                userState.ClearState();
            }));

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
            Assert.False(userState.HasValue("key"));
        }

        [Fact]
        public async Task State_ConcurrentSaveChanges_ShouldNotCorruptData()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act - Multiple threads modifying state and saving concurrently
            var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
            {
                userState.SetValue($"key{i}", $"value{i}");
                await userState.SaveChangesAsync(context);
            }));

            await Task.WhenAll(tasks);

            // Assert - Load fresh state and verify all values persisted
            var userState2 = new UserState(storage);
            await userState2.LoadAsync(context);
            
            for (int i = 0; i < 20; i++)
            {
                var value = userState2.GetValue<string>($"key{i}");
                Assert.Equal($"value{i}", value);
            }
        }

        [Fact]
        public async Task State_OverlappingTurns_WithLoadedVersion_ShouldRejectStaleSave()
        {
            // Each turn has a distinct state instance but addresses the same conversation record.
            // The later turn must win when it saves before the earlier turn completes.
            var storage = new MemoryStorage();
            var initialState = new ConversationState(storage);
            var initialContext = TestUtilities.CreateEmptyContext();
            await initialState.LoadAsync(initialContext);
            initialState.SetValue("lastWriter", "initial");
            await initialState.SaveChangesAsync(initialContext);

            var slowTurnState = new ConversationState(storage);
            var fastTurnState = new ConversationState(storage);
            var slowTurnContext = TestUtilities.CreateEmptyContext();
            var fastTurnContext = TestUtilities.CreateEmptyContext();

            await slowTurnState.LoadAsync(slowTurnContext);
            await fastTurnState.LoadAsync(fastTurnContext);

            slowTurnState.SetValue("lastWriter", "slow");
            fastTurnState.SetValue("lastWriter", "fast");

            await fastTurnState.SaveChangesAsync(fastTurnContext);
            await Assert.ThrowsAsync<EtagException>(() => slowTurnState.SaveChangesAsync(slowTurnContext));

            var persistedState = new ConversationState(storage);
            var verificationContext = TestUtilities.CreateEmptyContext();
            await persistedState.LoadAsync(verificationContext);

            Assert.Equal("fast", persistedState.GetValue<string>("lastWriter"));
        }

        [Fact]
        public async Task State_DeleteAfterLoad_ShouldNotSaveDeletedStateAtEndOfTurn()
        {
            var storage = new MemoryStorage();
            var seed = new ConversationState(storage);
            var seedContext = TestUtilities.CreateEmptyContext();
            await seed.LoadAsync(seedContext);
            seed.SetValue("key", "value");
            await seed.SaveChangesAsync(seedContext);

            var state = new ConversationState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await state.LoadAsync(context);

            await state.DeleteStateAsync(context);
            await state.SaveChangesAsync(context);

            var reloadedState = new ConversationState(storage);
            var reloadContext = TestUtilities.CreateEmptyContext();
            await reloadedState.LoadAsync(reloadContext);
            Assert.False(reloadedState.HasValue("key"));
        }

        [Fact]
        public async Task State_OverlappingTurns_WithoutLoadedVersion_ShouldPreserveUpsertBehavior()
        {
            var storage = new MemoryStorage();
            var slowTurnState = new ConversationState(storage);
            var fastTurnState = new ConversationState(storage);
            var slowTurnContext = TestUtilities.CreateEmptyContext();
            var fastTurnContext = TestUtilities.CreateEmptyContext();

            await slowTurnState.LoadAsync(slowTurnContext);
            await fastTurnState.LoadAsync(fastTurnContext);

            slowTurnState.SetValue("lastWriter", "slow");
            fastTurnState.SetValue("lastWriter", "fast");

            await fastTurnState.SaveChangesAsync(fastTurnContext);
            await slowTurnState.SaveChangesAsync(slowTurnContext);

            var persistedState = new ConversationState(storage);
            var verificationContext = TestUtilities.CreateEmptyContext();
            await persistedState.LoadAsync(verificationContext);

            Assert.Equal("slow", persistedState.GetValue<string>("lastWriter"));
        }

        [Fact]
        public async Task State_LegacyStorage_ShouldSaveWithoutV2WriteOptions()
        {
            var storage = new Moq.Mock<IStorage>();
            storage.Setup(s => s.ReadAsync(Moq.It.IsAny<string[]>(), Moq.It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()));
            storage.Setup(s => s.WriteAsync(Moq.It.IsAny<IDictionary<string, object>>(), Moq.It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var state = new ConversationState(storage.Object);
            var context = TestUtilities.CreateEmptyContext();
            await state.LoadAsync(context);
            state.SetValue("key", "value");

            await state.SaveChangesAsync(context);

            storage.Verify(
                s => s.WriteAsync(Moq.It.IsAny<IDictionary<string, object>>(), Moq.It.IsAny<CancellationToken>()),
                Moq.Times.Once);
        }

        [Fact]
        public async Task State_MemoryStorageV2_ShouldPreserveUserETagProperty()
        {
            var storage = new MemoryStorage();
            var state = new ConversationState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await state.LoadAsync(context);

            state.SetValue("ETag", "user-value");
            await state.SaveChangesAsync(context);

            var reloadedState = new ConversationState(storage);
            var reloadContext = TestUtilities.CreateEmptyContext();
            await reloadedState.LoadAsync(reloadContext);

            Assert.Equal("user-value", reloadedState.GetValue<string>("ETag"));
        }

        [Fact]
        public async Task State_SaveChangesAfterInitialSave_ShouldUseNewVersion()
        {
            const string firstVersion = "version-1";
            string expectedVersion = null;
            var storage = new Moq.Mock<IStorageV2>();
            storage.Setup(s => s.ReadAsync(Moq.It.IsAny<IReadOnlyList<string>>(), Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<string> keys, CancellationToken _) =>
                    new Dictionary<string, StorageReadResult>
                    {
                        [keys[0]] = new StorageReadResult { Key = keys[0], Status = StorageOperationStatus.NotFound },
                    });
            storage.Setup(s => s.WriteAsync(
                    Moq.It.IsAny<IReadOnlyDictionary<string, object>>(),
                    Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyDictionary<string, object> changes, CancellationToken _) =>
                    new Dictionary<string, StorageWriteResult>
                    {
                        [changes.Keys.Single()] = new StorageWriteResult
                        {
                            Key = changes.Keys.Single(),
                            Status = StorageOperationStatus.Succeeded,
                            Version = firstVersion,
                        },
                    });
            storage.Setup(s => s.WriteAsync(
                    Moq.It.IsAny<IReadOnlyDictionary<string, object>>(),
                    Moq.It.IsAny<StorageWriteOptions>(),
                    Moq.It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyDictionary<string, object> _, StorageWriteOptions options, CancellationToken _) =>
                    expectedVersion = options.ExpectedVersion)
                .ReturnsAsync((IReadOnlyDictionary<string, object> changes, StorageWriteOptions _, CancellationToken _) =>
                    new Dictionary<string, StorageWriteResult>
                    {
                        [changes.Keys.Single()] = new StorageWriteResult
                        {
                            Key = changes.Keys.Single(),
                            Status = StorageOperationStatus.Succeeded,
                            Version = "version-2",
                        },
                    });

            var state = new ConversationState(storage.Object);
            var context = TestUtilities.CreateEmptyContext();
            await state.LoadAsync(context);

            state.SetValue("first", "value1");
            await state.SaveChangesAsync(context);

            state.SetValue("second", "value2");
            await state.SaveChangesAsync(context);

            Assert.Equal(firstVersion, expectedVersion);
        }

        [Fact]
        public async Task State_SuccessfulReadWithoutVersion_ShouldUseUpsert()
        {
            var storage = new Mock<IStorageV2>();
            storage.Setup(s => s.ReadAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<string> keys, CancellationToken _) =>
                    new Dictionary<string, StorageReadResult>
                    {
                        [keys[0]] = new StorageReadResult
                        {
                            Key = keys[0],
                            Status = StorageOperationStatus.Succeeded,
                            Value = new Dictionary<string, object> { ["existing"] = "value" },
                            Version = null,
                        },
                    });
            storage.Setup(s => s.WriteAsync(
                    It.IsAny<IReadOnlyDictionary<string, object>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyDictionary<string, object> changes, CancellationToken _) =>
                    new Dictionary<string, StorageWriteResult>
                    {
                        [changes.Keys.Single()] = new StorageWriteResult
                        {
                            Key = changes.Keys.Single(),
                            Status = StorageOperationStatus.Succeeded,
                            Version = "version-1",
                        },
                    });

            var state = new ConversationState(storage.Object);
            var context = TestUtilities.CreateEmptyContext();
            await state.LoadAsync(context);
            state.SetValue("changed", "value");

            await state.SaveChangesAsync(context);

            storage.Verify(s => s.WriteAsync(
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            storage.Verify(s => s.WriteAsync(
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<StorageWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task State_ConcurrentTryGetValue_ShouldNotThrow()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);
            userState.SetValue("key1", "value1");

            // Act - Multiple threads calling TryGetValue
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var found = userState.TryGetValue("key1", out string value);
                    Assert.True(found);
                    Assert.Equal("value1", value);

                    var notFound = userState.TryGetValue("nonexistent", out string missing);
                    Assert.False(notFound);
                    Assert.Null(missing);
                }
            }));

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task State_SaveChangesDuringConcurrentModifications_ShouldCaptureSomeState()
        {
            // Arrange
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var context = TestUtilities.CreateEmptyContext();
            await userState.LoadAsync(context);

            // Act - Some threads modifying, one saving
            var modifyTasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                userState.SetValue($"key{i}", $"value{i}");
                Thread.Sleep(1); // Small delay to interleave operations
            }));

            var saveTask = Task.Run(async () =>
            {
                Thread.Sleep(50); // Let some modifications happen first
                await userState.SaveChangesAsync(context);
            });

            await Task.WhenAll(modifyTasks.Concat(new[] { saveTask }));

            // Assert - State should be consistent (no corruption)
            // Note: We don't assert on specific keys since timing is non-deterministic,
            // but the state should be valid and contain some data
            var userState2 = new UserState(storage);
            await userState2.LoadAsync(context);
            // Just verify we can iterate without exceptions
            var hasAnyValue = false;
            for (int i = 0; i < 100; i++)
            {
                if (userState2.TryGetValue($"key{i}", out string _))
                {
                    hasAnyValue = true;
                    break;
                }
            }
            Assert.True(hasAnyValue);
        }
    }
}
