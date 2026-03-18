// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A.Tests
{
    public class StorageTaskStoreTests
    {
        private readonly IStorage _storage;
        private readonly StorageTaskStore _taskStore;

        public StorageTaskStoreTests()
        {
            _storage = new MemoryStorage(); 
            _taskStore = new StorageTaskStore(_storage);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullStorage_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new StorageTaskStore(null));
        }

        [Fact]
        public void Constructor_WithValidStorage_Success()
        {
            // Arrange & Act
            var storage = new Mock<IStorage>();
            var taskStore = new StorageTaskStore(storage.Object);

            // Assert
            Assert.NotNull(taskStore);
        }

        #endregion

        #region GetTaskAsync Tests

        [Fact]
        public async Task GetTaskAsync_WithNullTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _taskStore.GetTaskAsync(null));
        }

        [Fact]
        public async Task GetTaskAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _taskStore.GetTaskAsync(string.Empty));
        }

        [Fact]
        public async Task GetTaskAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.GetTaskAsync("task-123", cts.Token));
        }

        [Fact]
        public async Task GetTaskAsync_WhenTaskExists_ReturnsTask()
        {
            // Arrange
            var taskId = "task-123";
            var expectedTask = new AgentTask
            {
                Id = taskId,
                Status = new()
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            var storageData = new Dictionary<string, object>
            {
                { $"a2atask/{taskId}", expectedTask }
            };
            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.GetTaskAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskId, result.Id);
            Assert.Equal(TaskState.Working, result.Status.State);
        }

        [Fact]
        public async Task GetTaskAsync_WhenTaskDoesNotExist_ReturnsNull()
        {
            // Arrange
            var taskId = "task-123";

            // Act
            var result = await _taskStore.GetTaskAsync(taskId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTaskAsync_WhenStorageReturnsWrongType_ReturnsNull()
        {
            // Arrange
            var taskId = "task-123";
            var storageData = new Dictionary<string, object>
            {
                { $"a2atask/{taskId}", new TokenResponse() }
            };
            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.GetTaskAsync(taskId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region UpdateStatusAsync Tests

        [Fact]
        public async Task UpdateStatusAsync_WithNullTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.SaveTaskAsync(null, new AgentTask()));
        }

        [Fact]
        public async Task UpdateStatusAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.SaveTaskAsync(string.Empty, new AgentTask()));
        }

        [Fact]
        public async Task UpdateStatusAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.SaveTaskAsync("task-123", new AgentTask(), cts.Token));
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenTaskNotTerminal_UpdatesStatusAndWritesToStorage()
        {
            // Arrange
            var taskId = "task-123";
            var existingTask = new AgentTask
            {
                Id = taskId,
                Status = new()
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            };

            var storageData = new Dictionary<string, object>
            {
                { $"a2atask/{taskId}", existingTask }
            };

            await _storage.WriteAsync(storageData, CancellationToken.None);

            var task = await _taskStore.GetTaskAsync(taskId);
            Assert.NotNull(task);

            // Act
            var message = new Message
            {
                MessageId = "msg-123",
                TaskId = taskId,
                ContextId = "context-123"
            };

            task.Status.State = TaskState.InputRequired;
            task.Status.Message = message;

            await _taskStore.SaveTaskAsync(taskId, task);

            // Assert
            task = await _taskStore.GetTaskAsync(taskId);
            Assert.Equal(TaskState.InputRequired, task.Status.State);
            Assert.Equal(message.MessageId, task.Status.Message.MessageId);
        }

        #endregion

        #region SetTaskAsync Tests

        [Fact]
        public async Task SetTaskAsync_WithNullTask_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _taskStore.SaveTaskAsync("task-123", null));
        }

        [Fact]
        public async Task SetTaskAsync_WithNullTaskId_ThrowsA2AException()
        {
            // Arrange
            var task = new AgentTask { Id = null };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _taskStore.SaveTaskAsync(null, task));
        }

        [Fact]
        public async Task SetTaskAsync_WithEmptyTaskId_ThrowsA2AException()
        {
            // Arrange
            var task = new AgentTask { Id = string.Empty };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _taskStore.SaveTaskAsync(string.Empty, task));
        }

        [Fact]
        public async Task SetTaskAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var task = new AgentTask { Id = "task-123" };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.SaveTaskAsync("task-123", task, cts.Token));
        }

        [Fact]
        public async Task SetTaskAsync_WithValidTask_WritesToStorage()
        {
            // Arrange
            var task = new AgentTask
            {
                Id = "task-123",
                Status = new()
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            // Act
            await _taskStore.SaveTaskAsync("task-123", task);

            var items = await _storage.ReadAsync(new[] { $"a2atask/{task.Id}" }, CancellationToken.None);
            Assert.NotEmpty(items);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task IntegrationTest_SetAndGetTask_RoundTrip()
        {
            // Arrange
            var storage = new MemoryStorage();
            var taskStore = new StorageTaskStore(storage);
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

            // Act
            await taskStore.SaveTaskAsync("task-integration", task);
            var retrievedTask = await taskStore.GetTaskAsync(task.Id);

            // Assert
            Assert.NotNull(retrievedTask);
            Assert.Equal(task.Id, retrievedTask.Id);
            Assert.Equal(task.Status.State, retrievedTask.Status.State);
        }

        [Fact]
        public async Task IntegrationTest_SaveTaskStatus_RoundTrip()
        {
            // Arrange
            var storage = new MemoryStorage();
            var taskStore = new StorageTaskStore(storage);
            var task = new AgentTask
            {
                Id = "task-update",
                Status = new()
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            await taskStore.SaveTaskAsync("task-update", task);

            var message = new Message
            {
                MessageId = "msg-update",
                TaskId = task.Id,
                ContextId = "context-update"
            };
            task.Status.State = TaskState.InputRequired;
            task.Status.Message = message;

            // Act
            await taskStore.SaveTaskAsync(task.Id, task);
            var retrievedTask = await taskStore.GetTaskAsync(task.Id);

            // Assert
            Assert.Equal(TaskState.InputRequired, retrievedTask.Status.State);
            Assert.Equal(ProtocolJsonSerializer.ToJson(message), ProtocolJsonSerializer.ToJson(retrievedTask.Status.Message));
            Assert.Equal(TaskState.InputRequired, retrievedTask.Status.State);
        }

        #endregion
    }
}