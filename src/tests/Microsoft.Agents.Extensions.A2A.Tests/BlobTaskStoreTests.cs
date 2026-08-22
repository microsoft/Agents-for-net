// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Azure.Storage.Blobs;
using Microsoft.Agents.Extensions.A2A;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

// These tests require Azure Storage Emulator v5.7 or Azurite
// The emulator must be running and accessible at the default endpoint
// More info: https://docs.microsoft.com/azure/storage/common/storage-use-emulator
//
// Enable with env var: XUNITAZURESTORAGETESTENABLED=1
namespace Microsoft.Agents.Extensions.A2A.Tests
{
    public class BlobTaskStoreTests : IAsyncLifetime
    {
        private const string ConnectionString = @"AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
        private readonly string _testName;
        private string ContainerName => $"a2atasks{_testName.ToLower().Replace("_", string.Empty)}";

        public BlobTaskStoreTests(ITestOutputHelper testOutputHelper)
        {
            var helper = (TestOutputHelper)testOutputHelper;

            var test = (ITest)helper.GetType().GetField("test", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(helper);

            _testName = test.TestCase.TestMethod.Method.Name;

            if (StorageEmulatorHelper.CheckEmulator())
            {
                new BlobContainerClient(ConnectionString, ContainerName)
                    .DeleteIfExistsAsync().ConfigureAwait(false);
            }
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                await new BlobContainerClient(ConnectionString, ContainerName)
                    .DeleteIfExistsAsync().ConfigureAwait(false);
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new BlobTaskStore(null, ContainerName));
            }
        }

        [Fact]
        public void Constructor_WithNullContainerName_ThrowsArgumentNullException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new BlobTaskStore(ConnectionString, null));
            }
        }

        [Fact]
        public void Constructor_WithEmptyConnectionString_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new BlobTaskStore(string.Empty, ContainerName));
            }
        }

        [Fact]
        public void Constructor_WithEmptyContainerName_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new BlobTaskStore(ConnectionString, string.Empty));
            }
        }

        [Fact]
        public void Constructor_WithValidParameters_Success()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                Assert.NotNull(taskStore);
            }
        }

        [Fact]
        public void Constructor_WithBlobContainerClient_Success()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var containerClient = new BlobContainerClient(ConnectionString, ContainerName);
                var taskStore = new BlobTaskStore(containerClient);
                Assert.NotNull(taskStore);
            }
        }

        #endregion

        #region GetTaskAsync Tests

        [Fact]
        public async Task GetTaskAsync_WithNullTaskId_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await Assert.ThrowsAsync<ArgumentNullException>(() => taskStore.GetTaskAsync(null));
            }
        }

        [Fact]
        public async Task GetTaskAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await Assert.ThrowsAsync<ArgumentException>(() => taskStore.GetTaskAsync(string.Empty));
            }
        }

        [Fact]
        public async Task GetTaskAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var cts = new CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    taskStore.GetTaskAsync("task-123", cts.Token));
            }
        }

        [Fact]
        public async Task GetTaskAsync_WhenTaskExists_ReturnsTask()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var taskId = "task-exists-123";
                var expectedTask = new AgentTask
                {
                    Id = taskId,
                    Status = new()
                    {
                        State = TaskState.Working,
                        Timestamp = DateTimeOffset.UtcNow
                    }
                };

                await taskStore.SaveTaskAsync(taskId, expectedTask);

                var result = await taskStore.GetTaskAsync(taskId);

                Assert.NotNull(result);
                Assert.Equal(taskId, result.Id);
                Assert.Equal(TaskState.Working, result.Status.State);
            }
        }

        [Fact]
        public async Task GetTaskAsync_WhenTaskDoesNotExist_ReturnsNull()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var taskId = "task-nonexistent-123";

                var result = await taskStore.GetTaskAsync(taskId);

                Assert.Null(result);
            }
        }

        #endregion

        #region SaveTaskAsync Tests

        [Fact]
        public async Task SaveTaskAsync_WithNullTask_ThrowsArgumentNullException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await Assert.ThrowsAsync<ArgumentNullException>(() => taskStore.SaveTaskAsync("task-123", null));
            }
        }

        [Fact]
        public async Task SaveTaskAsync_WithNullTaskId_ThrowsArgumentNullException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var task = new AgentTask { Id = null };

                await Assert.ThrowsAsync<ArgumentNullException>(() => taskStore.SaveTaskAsync(null, task));
            }
        }

        [Fact]
        public async Task SaveTaskAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var task = new AgentTask { Id = string.Empty };

                await Assert.ThrowsAsync<ArgumentException>(() => taskStore.SaveTaskAsync(string.Empty, task));
            }
        }

        [Fact]
        public async Task SaveTaskAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var task = new AgentTask { Id = "task-123" };
                var cts = new CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    taskStore.SaveTaskAsync("task-123", task, cts.Token));
            }
        }

        [Fact]
        public async Task SaveTaskAsync_WithValidTask_SavesSuccessfully()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var task = new AgentTask
                {
                    Id = "task-save-123",
                    Status = new()
                    {
                        State = TaskState.Working,
                        Timestamp = DateTimeOffset.UtcNow
                    }
                };

                await taskStore.SaveTaskAsync("task-save-123", task);

                var result = await taskStore.GetTaskAsync("task-save-123");
                Assert.NotNull(result);
                Assert.Equal("task-save-123", result.Id);
            }
        }

        [Fact]
        public async Task SaveTaskAsync_UpdateExistingTask_UpdatesSuccessfully()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var taskId = "task-update-123";
                var task = new AgentTask
                {
                    Id = taskId,
                    Status = new()
                    {
                        State = TaskState.Working,
                        Timestamp = DateTimeOffset.UtcNow
                    }
                };

                await taskStore.SaveTaskAsync(taskId, task);

                task.Status.State = TaskState.InputRequired;
                task.Status.Message = new Message
                {
                    MessageId = "msg-123",
                    TaskId = taskId,
                    ContextId = "context-123"
                };

                await taskStore.SaveTaskAsync(taskId, task);

                var result = await taskStore.GetTaskAsync(taskId);
                Assert.Equal(TaskState.InputRequired, result.Status.State);
                Assert.Equal("msg-123", result.Status.Message.MessageId);
            }
        }

        #endregion

        #region DeleteTaskAsync Tests

        [Fact]
        public async Task DeleteTaskAsync_WithNullTaskId_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await Assert.ThrowsAsync<ArgumentNullException>(() => taskStore.DeleteTaskAsync(null));
            }
        }

        [Fact]
        public async Task DeleteTaskAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await Assert.ThrowsAsync<ArgumentException>(() => taskStore.DeleteTaskAsync(string.Empty));
            }
        }

        [Fact]
        public async Task DeleteTaskAsync_WhenTaskExists_DeletesSuccessfully()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var taskId = "task-delete-123";
                var task = new AgentTask
                {
                    Id = taskId,
                    Status = new() { State = TaskState.Working }
                };

                await taskStore.SaveTaskAsync(taskId, task);
                var beforeDelete = await taskStore.GetTaskAsync(taskId);
                Assert.NotNull(beforeDelete);

                await taskStore.DeleteTaskAsync(taskId);

                var afterDelete = await taskStore.GetTaskAsync(taskId);
                Assert.Null(afterDelete);
            }
        }

        [Fact]
        public async Task DeleteTaskAsync_WhenTaskDoesNotExist_DoesNotThrow()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await taskStore.DeleteTaskAsync("task-nonexistent");
            }
        }

        #endregion

        #region ListTasksAsync Tests

        [Fact]
        public async Task ListTasksAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                await Assert.ThrowsAsync<ArgumentNullException>(() => taskStore.ListTasksAsync(null));
            }
        }

        [Fact]
        public async Task ListTasksAsync_WhenNoTasks_ReturnsEmptyList()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var request = new ListTasksRequest();

                var result = await taskStore.ListTasksAsync(request);

                Assert.NotNull(result);
                Assert.Empty(result.Tasks);
                Assert.Null(result.NextPageToken);
            }
        }

        [Fact]
        public async Task ListTasksAsync_ReturnsAllTasks()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);

                // Create multiple tasks
                for (int i = 0; i < 5; i++)
                {
                    var task = new AgentTask
                    {
                        Id = $"task-{i}",
                        Status = new() { State = TaskState.Working }
                    };
                    await taskStore.SaveTaskAsync($"task-{i}", task);
                }

                var request = new ListTasksRequest();
                var result = await taskStore.ListTasksAsync(request);

                Assert.NotNull(result);
                Assert.Equal(5, result.Tasks.Count);
            }
        }

        [Fact]
        public async Task ListTasksAsync_WithPageSize_ReturnsLimitedResults()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);

                // Create multiple tasks
                for (int i = 0; i < 10; i++)
                {
                    var task = new AgentTask
                    {
                        Id = $"task-page-{i}",
                        Status = new() { State = TaskState.Working }
                    };
                    await taskStore.SaveTaskAsync($"task-page-{i}", task);
                }

                var request = new ListTasksRequest { PageSize = 3 };
                var result = await taskStore.ListTasksAsync(request);

                Assert.NotNull(result);
                Assert.True(result.Tasks.Count <= 3);
                Assert.NotNull(result.NextPageToken); // Should have more results
            }
        }

        [Fact]
        public async Task ListTasksAsync_WithContinuationToken_ReturnsNextPage()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);

                // Create multiple tasks
                for (int i = 0; i < 10; i++)
                {
                    var task = new AgentTask
                    {
                        Id = $"task-continuation-{i}",
                        Status = new() { State = TaskState.Working }
                    };
                    await taskStore.SaveTaskAsync($"task-continuation-{i}", task);
                }

                // Get first page
                var firstRequest = new ListTasksRequest { PageSize = 3 };
                var firstResult = await taskStore.ListTasksAsync(firstRequest);

                Assert.NotNull(firstResult.NextPageToken);

                // Get second page
                var secondRequest = new ListTasksRequest
                {
                    PageSize = 3,
                    PageToken = firstResult.NextPageToken
                };
                var secondResult = await taskStore.ListTasksAsync(secondRequest);

                Assert.NotNull(secondResult);
                // Results should be different pages
                Assert.NotEqual(firstResult.Tasks[0].Id, secondResult.Tasks[0].Id);
            }
        }

        [Fact]
        public async Task ListTasksAsync_WithStatusFilter_ReturnsFilteredTasks()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);

                // Create tasks with different statuses
                await taskStore.SaveTaskAsync("task-working-1", new AgentTask
                {
                    Id = "task-working-1",
                    Status = new() { State = TaskState.Working }
                });

                await taskStore.SaveTaskAsync("task-completed-1", new AgentTask
                {
                    Id = "task-completed-1",
                    Status = new() { State = TaskState.Completed }
                });

                await taskStore.SaveTaskAsync("task-working-2", new AgentTask
                {
                    Id = "task-working-2",
                    Status = new() { State = TaskState.Working }
                });

                var request = new ListTasksRequest { Status = TaskState.Working };
                var result = await taskStore.ListTasksAsync(request);

                Assert.NotNull(result);
                Assert.Equal(2, result.Tasks.Count);
                Assert.All(result.Tasks, task => Assert.Equal(TaskState.Working, task.Status.State));
            }
        }

        [Fact]
        public async Task ListTasksAsync_WithContextIdFilter_ReturnsFilteredTasks()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);

                // Create tasks with different context IDs
                await taskStore.SaveTaskAsync("task-ctx-a-1", new AgentTask
                {
                    Id = "task-ctx-a-1",
                    ContextId = "context-A",
                    Status = new() { State = TaskState.Working }
                });

                await taskStore.SaveTaskAsync("task-ctx-b-1", new AgentTask
                {
                    Id = "task-ctx-b-1",
                    ContextId = "context-B",
                    Status = new() { State = TaskState.Working }
                });

                await taskStore.SaveTaskAsync("task-ctx-a-2", new AgentTask
                {
                    Id = "task-ctx-a-2",
                    ContextId = "context-A",
                    Status = new() { State = TaskState.Working }
                });

                var request = new ListTasksRequest { ContextId = "context-A" };
                var result = await taskStore.ListTasksAsync(request);

                Assert.NotNull(result);
                Assert.Equal(2, result.Tasks.Count);
                Assert.All(result.Tasks, task => Assert.Equal("context-A", task.ContextId));
            }
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task IntegrationTest_SaveGetDeleteTask_RoundTrip()
        {
            if (StorageEmulatorHelper.CheckEmulator())
            {
                var taskStore = new BlobTaskStore(ConnectionString, ContainerName);
                var task = new AgentTask
                {
                    Id = "task-integration",
                    Status = new()
                    {
                        State = TaskState.Working,
                        Timestamp = DateTimeOffset.UtcNow,
                        Message = new Message
                        {
                            MessageId = "msg-123",
                            TaskId = "task-integration",
                            ContextId = "context-123"
                        }
                    }
                };

                // Save
                await taskStore.SaveTaskAsync("task-integration", task);

                // Get
                var retrievedTask = await taskStore.GetTaskAsync(task.Id);
                Assert.NotNull(retrievedTask);
                Assert.Equal(task.Id, retrievedTask.Id);
                Assert.Equal(task.Status.State, retrievedTask.Status.State);

                // Delete
                await taskStore.DeleteTaskAsync(task.Id);
                var deletedTask = await taskStore.GetTaskAsync(task.Id);
                Assert.Null(deletedTask);
            }
        }

        #endregion
    }
}
