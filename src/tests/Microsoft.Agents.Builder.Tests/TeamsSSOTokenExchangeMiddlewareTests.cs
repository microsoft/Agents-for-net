// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class TeamsSSOTokenExchangeMiddlewareTests
    {
        private const string ConnectionName = "connection-name";

        [Fact]
        public async Task OnTurnAsync_ShouldUseNativeV2CreateOnlySuccess()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var context = CreateTurnContext(CreateStorageTokenClient(), sentActivities);
            var storage = new NativeStorageStub(StorageOperationStatus.Succeeded);
            var middleware = new TeamsSSOTokenExchangeMiddleware(storage, ConnectionName);
            var nextCalled = false;

            // Act
            await middleware.OnTurnAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }, CancellationToken.None);

            // Assert
            Assert.True(nextCalled);
            Assert.True(storage.V2WriteCalled);
            Assert.False(storage.V1WriteCalled);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldUseNativeV2CreateOnlyConflict()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var context = CreateTurnContext(CreateStorageTokenClient(), sentActivities);
            var storage = new NativeStorageStub(StorageOperationStatus.Conflict);
            var middleware = new TeamsSSOTokenExchangeMiddleware(storage, ConnectionName);
            var nextCalled = false;

            // Act
            await middleware.OnTurnAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }, CancellationToken.None);

            // Assert
            Assert.False(nextCalled);
            Assert.True(storage.V2WriteCalled);
            Assert.False(storage.V1WriteCalled);
            var invokeResponse = Assert.Single(sentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, invokeResponse.Type);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)invokeResponse.Value).Status);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldFallbackToV1ConflictBehavior()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var context = CreateTurnContext(CreateStorageTokenClient(), sentActivities);
            var storage = new V1ConflictStorageStub();
            var middleware = new TeamsSSOTokenExchangeMiddleware(storage, ConnectionName);
            var nextCalled = false;

            // Act
            await middleware.OnTurnAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }, CancellationToken.None);

            // Assert
            Assert.False(nextCalled);
            Assert.True(storage.V1WriteCalled);
            var invokeResponse = Assert.Single(sentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, invokeResponse.Type);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)invokeResponse.Value).Status);
        }

        [Fact]
        public async Task OnTurnAsync_WithMemoryStorage_DeduplicatesDuplicateInvoke()
        {
            // Arrange
            var storage = new MemoryStorage();
            var middleware = new TeamsSSOTokenExchangeMiddleware(storage, ConnectionName);
            var nextCallCount = 0;

            var firstSentActivities = new List<IActivity>();
            var firstContext = CreateTurnContext(CreateStorageTokenClient(), firstSentActivities);

            var secondSentActivities = new List<IActivity>();
            var secondContext = CreateTurnContext(CreateStorageTokenClient(), secondSentActivities);

            // Act
            await middleware.OnTurnAsync(firstContext, _ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, CancellationToken.None);

            await middleware.OnTurnAsync(secondContext, _ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, CancellationToken.None);

            // Assert
            Assert.Equal(1, nextCallCount);
            Assert.Empty(firstSentActivities);
            var invokeResponse = Assert.Single(secondSentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, invokeResponse.Type);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)invokeResponse.Value).Status);
        }

        [Fact]
        public async Task OnTurnAsync_WithV1Storage_DeduplicatesDuplicateInvoke()
        {
            // Arrange
            var storage = new V1DeduplicatingStorageStub();
            var middleware = new TeamsSSOTokenExchangeMiddleware(storage, ConnectionName);
            var nextCallCount = 0;

            var firstSentActivities = new List<IActivity>();
            var firstContext = CreateTurnContext(CreateStorageTokenClient(), firstSentActivities);

            var secondSentActivities = new List<IActivity>();
            var secondContext = CreateTurnContext(CreateStorageTokenClient(), secondSentActivities);

            // Act
            await middleware.OnTurnAsync(firstContext, _ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, CancellationToken.None);

            await middleware.OnTurnAsync(secondContext, _ =>
            {
                nextCallCount++;
                return Task.CompletedTask;
            }, CancellationToken.None);

            // Assert
            Assert.Equal(1, nextCallCount);
            Assert.Equal(2, storage.WriteCallCount);
            Assert.Empty(firstSentActivities);
            var invokeResponse = Assert.Single(secondSentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, invokeResponse.Type);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)invokeResponse.Value).Status);
        }

        private static IUserTokenClient CreateStorageTokenClient()
        {
            var tokenClient = new Mock<IUserTokenClient>();
            tokenClient
                .Setup(c => c.ExchangeTokenAsync(
                    It.IsAny<string>(),
                    ConnectionName,
                    It.IsAny<ChannelId>(),
                    It.IsAny<TokenExchangeRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { Token = "sso-token" });
            return tokenClient.Object;
        }

        private static TurnContext CreateTurnContext(IUserTokenClient tokenClient, List<IActivity> sentActivities)
        {
            void CaptureActivities(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var context = new TurnContext(new SimpleAdapter(CaptureActivities), new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "exchange-id",
                    ConnectionName = ConnectionName,
                    Token = "teams-sso-token"
                }
            });

            context.Services.Set(tokenClient);
            return context;
        }

        private sealed class NativeStorageStub : IStorageV2
        {
            private readonly StorageOperationStatus _status;

            public NativeStorageStub(StorageOperationStatus status)
            {
                _status = status;
            }

            public bool V1WriteCalled { get; private set; }

            public bool V2WriteCalled { get; private set; }

            public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                throw new NotSupportedException();
            }

            public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
            {
                V1WriteCalled = true;
                throw new NotSupportedException("V1 write path should not be used when native V2 is available.");
            }

            public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                V1WriteCalled = true;
                throw new NotSupportedException("V1 write path should not be used when native V2 is available.");
            }

            public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyDictionary<string, StorageReadResult>> ReadAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, CancellationToken cancellationToken = default) where TValue : class
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, StorageWriteOptions options, CancellationToken cancellationToken = default) where TValue : class
            {
                V2WriteCalled = true;
                var key = changes.Keys.Single();
                return Task.FromResult<IReadOnlyDictionary<string, StorageWriteResult>>(new Dictionary<string, StorageWriteResult>
                {
                    [key] = new StorageWriteResult { Key = key, Status = _status }
                });
            }

            public Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, StorageDeleteOptions options, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class V1ConflictStorageStub : IStorage
        {
            public bool V1WriteCalled { get; private set; }

            public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                throw new NotSupportedException();
            }

            public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
            {
                V1WriteCalled = true;
                throw new Exception("Etag conflict: pre-condition is not met");
            }

            public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                V1WriteCalled = true;
                throw new Exception("Etag conflict: pre-condition is not met");
            }

            public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class V1DeduplicatingStorageStub : IStorage
        {
            private readonly Dictionary<string, string> _versions = new();
            private int _nextVersion;

            public int WriteCallCount { get; private set; }

            public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                throw new NotSupportedException();
            }

            public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
            {
                WriteCallCount++;
                foreach (var change in changes)
                {
                    var providedVersion = (change.Value as IStoreItem)?.ETag;
                    if (_versions.TryGetValue(change.Key, out var currentVersion)
                        && providedVersion != "*"
                        && !string.Equals(providedVersion, currentVersion, StringComparison.Ordinal))
                    {
                        throw new Exception("Etag conflict: pre-condition is not met");
                    }

                    _versions[change.Key] = $"server-version-{++_nextVersion}";
                }

                return Task.CompletedTask;
            }

            public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                Dictionary<string, object> changesAsObject = new(changes.Count);
                foreach (var change in changes)
                {
                    changesAsObject.Add(change.Key, change.Value);
                }

                return WriteAsync(changesAsObject, cancellationToken);
            }

            public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}