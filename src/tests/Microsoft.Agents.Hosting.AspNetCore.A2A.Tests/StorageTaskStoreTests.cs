// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Identity.Client.Extensions.Msal;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Agents.Hosting.AspNetCore.A2A.StorageTaskStore;

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
                Status = new AgentTaskStatus
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

        #region GetPushNotificationAsync Tests

        [Fact]
        public async Task GetPushNotificationAsync_WithNullTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskStore.GetPushNotificationAsync(null, "notification-123"));
        }

        [Fact]
        public async Task GetPushNotificationAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.GetPushNotificationAsync(string.Empty, "notification-123"));
        }

        [Fact]
        public async Task GetPushNotificationAsync_WithNullNotificationConfigId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskStore.GetPushNotificationAsync("task-123", null));
        }

        [Fact]
        public async Task GetPushNotificationAsync_WithEmptyNotificationConfigId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.GetPushNotificationAsync("task-123", string.Empty));
        }

        [Fact]
        public async Task GetPushNotificationAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.GetPushNotificationAsync("task-123", "notification-123", cts.Token));
        }

        [Fact]
        public async Task GetPushNotificationAsync_WhenNotificationExists_ReturnsNotification()
        {
            // Arrange
            var taskId = "task-123";
            var notificationConfigId = "notification-123";
            var expectedNotification = new TaskPushNotificationConfig
            {
                TaskId = taskId,
                PushNotificationConfig = new PushNotificationConfig
                {
                    Id = notificationConfigId,
                    Url = "https://example.com/webhook"
                }
            };

            var storageData = new Dictionary<string, object>
            {
                { $"a2apush/{taskId}", new PushNotifications([expectedNotification]) }
            };
            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.GetPushNotificationAsync(taskId, notificationConfigId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(taskId, result.TaskId);
            Assert.Equal(notificationConfigId, result.PushNotificationConfig.Id);
        }

        [Fact]
        public async Task GetPushNotificationAsync_WhenNotificationDoesNotExist_ReturnsNull()
        {
            // Arrange
            var taskId = "task-123";
            var notificationConfigId = "notification-123";
            var storageData = new Dictionary<string, object>
            {
                { $"a2apush/{taskId}", new PushNotifications() }
            };

            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.GetPushNotificationAsync(taskId, notificationConfigId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region UpdateStatusAsync Tests

        [Fact]
        public async Task UpdateStatusAsync_WithNullTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskStore.UpdateStatusAsync(null, TaskState.Working));
        }

        [Fact]
        public async Task UpdateStatusAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.UpdateStatusAsync(string.Empty, TaskState.Working));
        }

        [Fact]
        public async Task UpdateStatusAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.UpdateStatusAsync("task-123", TaskState.Working, null, cts.Token));
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenTaskNotFound_ThrowsA2AException()
        {
            // Arrange
            var taskId = "task-123";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<A2AException>(() =>
                _taskStore.UpdateStatusAsync(taskId, TaskState.Working));

            Assert.Equal("Task not found.", exception.Message);
            Assert.Equal(A2AErrorCode.TaskNotFound, exception.ErrorCode);
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenTaskNotTerminal_UpdatesStatusAndWritesToStorage()
        {
            // Arrange
            var taskId = "task-123";
            var message = new AgentMessage
            {
                MessageId = "msg-123",
                TaskId = taskId,
                ContextId = "context-123"
            };

            var existingTask = new AgentTask
            {
                Id = taskId,
                Status = new AgentTaskStatus
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

            // Act
            var result = await _taskStore.UpdateStatusAsync(taskId, TaskState.InputRequired, message);

            // Assert
            Assert.Equal(TaskState.InputRequired, result.State);
            Assert.Equal(message, result.Message);
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenTaskIsTerminal_DoesNotUpdateStatus()
        {
            // Arrange
            var taskId = "task-123";
            var existingTask = new AgentTask
            {
                Id = taskId,
                Status = new AgentTaskStatus
                {
                    State = TaskState.Completed,
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            };

            var storageData = new Dictionary<string, object>
            {
                { $"a2atask/{taskId}", existingTask }
            };

            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.UpdateStatusAsync(taskId, TaskState.Working);

            // Assert
            Assert.Equal(TaskState.Completed, result.State); // Should remain completed
        }

        #endregion

        #region SetTaskAsync Tests

        [Fact]
        public async Task SetTaskAsync_WithNullTask_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _taskStore.SetTaskAsync(null));
        }

        [Fact]
        public async Task SetTaskAsync_WithNullTaskId_ThrowsA2AException()
        {
            // Arrange
            var task = new AgentTask { Id = null };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<A2AException>(() => _taskStore.SetTaskAsync(task));
            Assert.Equal("Invalid task ID", exception.Message);
            Assert.Equal(A2AErrorCode.InvalidParams, exception.ErrorCode);
        }

        [Fact]
        public async Task SetTaskAsync_WithEmptyTaskId_ThrowsA2AException()
        {
            // Arrange
            var task = new AgentTask { Id = string.Empty };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<A2AException>(() => _taskStore.SetTaskAsync(task));
            Assert.Equal("Invalid task ID", exception.Message);
            Assert.Equal(A2AErrorCode.InvalidParams, exception.ErrorCode);
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
                _taskStore.SetTaskAsync(task, cts.Token));
        }

        [Fact]
        public async Task SetTaskAsync_WithValidTask_WritesToStorage()
        {
            // Arrange
            var task = new AgentTask
            {
                Id = "task-123",
                Status = new AgentTaskStatus
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            // Act
            await _taskStore.SetTaskAsync(task);

            var items = await _storage.ReadAsync(new[] { $"a2atask/{task.Id}" }, CancellationToken.None);
            Assert.NotEmpty(items);
        }

        #endregion

        #region SetPushNotificationConfigAsync Tests

        [Fact]
        public async Task SetPushNotificationConfigAsync_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskStore.SetPushNotificationConfigAsync(null));
        }

        [Fact]
        public async Task SetPushNotificationConfigAsync_WithNullTaskId_ThrowsArgumentException()
        {
            // Arrange
            var config = new TaskPushNotificationConfig
            {
                TaskId = null,
                PushNotificationConfig = new PushNotificationConfig()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskStore.SetPushNotificationConfigAsync(config));
        }

        [Fact]
        public async Task SetPushNotificationConfigAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Arrange
            var config = new TaskPushNotificationConfig
            {
                TaskId = string.Empty,
                PushNotificationConfig = new PushNotificationConfig()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.SetPushNotificationConfigAsync(config));
        }

        [Fact]
        public async Task SetPushNotificationConfigAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var config = new TaskPushNotificationConfig
            {
                TaskId = "task-123",
                PushNotificationConfig = new PushNotificationConfig()
            };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.SetPushNotificationConfigAsync(config, cts.Token));
        }

        [Fact]
        public async Task SetPushNotificationConfigAsync_WhenConfigsExist_AddsToList()
        {
            // Arrange
            var taskId = "task-123";
            var existingConfig = new TaskPushNotificationConfig
            {
                TaskId = taskId,
                PushNotificationConfig = new PushNotificationConfig
                {
                    Id = "notification-1",
                    Url = "https://example.com/webhook1"
                }
            };

            var newConfig = new TaskPushNotificationConfig
            {
                TaskId = taskId,
                PushNotificationConfig = new PushNotificationConfig
                {
                    Id = "notification-2",
                    Url = "https://example.com/webhook2"
                }
            };

            var storageData = new Dictionary<string, object>
            {
                { $"a2apush/{taskId}", new PushNotifications([existingConfig])}
            };

            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            await _taskStore.SetPushNotificationConfigAsync(newConfig);

            var items = await _storage.ReadAsync(new[] { $"a2apush/{taskId}" }, CancellationToken.None);
            Assert.Single(items);
            var notifications = items[$"a2apush/{taskId}"] as PushNotifications;
            Assert.NotNull(notifications);
            Assert.Equal(2, notifications.Configs.Count);
        }

        #endregion

        #region GetPushNotificationsAsync Tests

        [Fact]
        public async Task GetPushNotificationsAsync_WithNullTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskStore.GetPushNotificationsAsync(null));
        }

        [Fact]
        public async Task GetPushNotificationsAsync_WithEmptyTaskId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskStore.GetPushNotificationsAsync(string.Empty));
        }

        [Fact]
        public async Task GetPushNotificationsAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _taskStore.GetPushNotificationsAsync("task-123", cts.Token));
        }

        [Fact]
        public async Task GetPushNotificationsAsync_WhenConfigsExist_ReturnsConfigs()
        {
            // Arrange
            var taskId = "task-123";
            var expectedConfigs = new PushNotifications(new List<TaskPushNotificationConfig>
            {
                new TaskPushNotificationConfig
                {
                    TaskId = taskId,
                    PushNotificationConfig = new PushNotificationConfig
                    {
                        Id = "notification-1",
                        Url = "https://example.com/webhook1"
                    }
                },
                new TaskPushNotificationConfig
                {
                    TaskId = taskId,
                    PushNotificationConfig = new PushNotificationConfig
                    {
                        Id = "notification-2",
                        Url = "https://example.com/webhook2"
                    }
                }
            });

            var storageData = new Dictionary<string, object>
            {
                { $"a2apush/{taskId}", expectedConfigs }
            };
            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.GetPushNotificationsAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Equal("notification-1", result.First().PushNotificationConfig.Id);
            Assert.Equal("notification-2", result.Last().PushNotificationConfig.Id);
        }

        [Fact]
        public async Task GetPushNotificationsAsync_WhenNoConfigsExist_ReturnsEmptyList()
        {
            // Arrange
            var taskId = "task-123";
            // Act
            var result = await _taskStore.GetPushNotificationsAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetPushNotificationsAsync_WhenStorageReturnsWrongType_ReturnsEmptyList()
        {
            // Arrange
            var taskId = "task-123";
            var storageData = new Dictionary<string, object>
            {
                { $"a2apush/{taskId}", new TokenResponse() }
            };
            await _storage.WriteAsync(storageData, CancellationToken.None);

            // Act
            var result = await _taskStore.GetPushNotificationsAsync(taskId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
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
                Status = new AgentTaskStatus
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow,
                    Message = new AgentMessage
                    {
                        MessageId = "msg-123",
                        TaskId = "task-integration",
                        ContextId = "context-123"
                    }
                }
            };

            // Act
            await taskStore.SetTaskAsync(task);
            var retrievedTask = await taskStore.GetTaskAsync(task.Id);

            // Assert
            Assert.NotNull(retrievedTask);
            Assert.Equal(task.Id, retrievedTask.Id);
            Assert.Equal(task.Status.State, retrievedTask.Status.State);
        }

        [Fact]
        public async Task IntegrationTest_UpdateTaskStatus_RoundTrip()
        {
            // Arrange
            var storage = new MemoryStorage();
            var taskStore = new StorageTaskStore(storage);
            var task = new AgentTask
            {
                Id = "task-update",
                Status = new AgentTaskStatus
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            await taskStore.SetTaskAsync(task);

            var message = new AgentMessage
            {
                MessageId = "msg-update",
                TaskId = task.Id,
                ContextId = "context-update"
            };

            // Act
            var updatedStatus = await taskStore.UpdateStatusAsync(task.Id, TaskState.InputRequired, message);
            var retrievedTask = await taskStore.GetTaskAsync(task.Id);

            // Assert
            Assert.Equal(TaskState.InputRequired, updatedStatus.State);
            Assert.Equal(message, updatedStatus.Message);
            Assert.Equal(TaskState.InputRequired, retrievedTask.Status.State);
        }

        [Fact]
        public async Task IntegrationTest_SetAndGetPushNotifications_RoundTrip()
        {
            // Arrange
            var storage = new MemoryStorage();
            var taskStore = new StorageTaskStore(storage);
            var taskId = "task-push";

            var config = new TaskPushNotificationConfig
            {
                TaskId = taskId,
                PushNotificationConfig = new PushNotificationConfig
                {
                    Id = "notification-roundtrip",
                    Url = "https://example.com/webhook"
                }
            };

            // Act
            await taskStore.SetPushNotificationConfigAsync(config);
            var retrievedConfigs = await taskStore.GetPushNotificationsAsync(taskId);
            var specificConfig = await taskStore.GetPushNotificationAsync(taskId, config.PushNotificationConfig.Id);

            // Assert
            Assert.NotNull(retrievedConfigs);
            Assert.Single(retrievedConfigs);
            Assert.Equal(config.PushNotificationConfig.Id, retrievedConfigs.First().PushNotificationConfig.Id);
            Assert.NotNull(specificConfig);
            Assert.Equal(config.PushNotificationConfig.Id, specificConfig.PushNotificationConfig.Id);
        }

        #endregion
    }
}