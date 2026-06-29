// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.SharePoint;
using Microsoft.Agents.Extensions.SharePoint.Compat;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Tests.SharePoint
{
    public class SharePointSSOTokenExchangeMiddlewareTests
    {
        private const string ConnectionName = "ConnectionName";
        private const string FakeExchangeableItem = "Fake token";
        private const string ExceptionExpected = "ExceptionExpected";
        private const string UserId = "user-id";
        private const string UserName = "user-name";
        private const string Token = "token";
        private readonly Mock<IStorage> _storage;
        private IActivity[] _activitiesToSend = null;

        private static readonly object _request = new { Data = FakeExchangeableItem, Properties = "Token", id = "test-id" };

        private readonly Activity _activity = new ()
        {
            Type = ActivityTypes.Invoke,
            Name = "cardExtension/token",
            ChannelId = Channels.M365,
            From = new ChannelAccount(UserId, UserName),
            Conversation = new ConversationAccount(id: "test-conversation"),
            Value = ProtocolJsonSerializer.ToObject<JsonElement>(_request)
        };

        public SharePointSSOTokenExchangeMiddlewareTests()
        {
            _storage = new Mock<IStorage>();
            _activitiesToSend = null;
        }

        void CaptureSend(IActivity[] arg)
        {
            _activitiesToSend = arg;
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullStorage()
        {
            Assert.Throws<ArgumentNullException>(() => new SharePointSSOTokenExchangeMiddleware(null, ConnectionName));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnectionName()
        {
            Assert.Throws<ArgumentNullException>(() => new SharePointSSOTokenExchangeMiddleware(_storage.Object, null));
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var middleware = new SharePointSSOTokenExchangeMiddleware(_storage.Object, ConnectionName);

            Assert.NotNull(middleware);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldExchangeToken()
        {
            // Arrange
            var adapter = new SharePointSSOAdapter(CaptureSend);
            adapter.AddExchangeableToken(ConnectionName, Channels.M365, UserId, FakeExchangeableItem, Token);

            var turnContext = adapter.CreateTurnContext(_activity);

            // Act
            var bot = new TestActivityHandler(_storage.Object);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldNotExchangeToken()
        {
            // Arrange
            var adapter = new SharePointSSOAdapter(CaptureSend);

            // since this test does not setup adapter.AddExchangeableToken, the exchange will not happen.

            var turnContext = adapter.CreateTurnContext(_activity);

            // Act
            var bot = new TestActivityHandler(_storage.Object);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldNotExchangeTokenOnDifferentChannel()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/token",
                ChannelId = Channels.Directline,
                From = new ChannelAccount(UserId, UserName),
                Conversation = new ConversationAccount(id: "test-conversation"),
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(_request)
            };

            var adapter = new SharePointSSOAdapter(CaptureSend);
            adapter.AddExchangeableToken(ConnectionName, Channels.Directline, UserId, FakeExchangeableItem, Token);

            var turnContext = adapter.CreateTurnContext(activity);

            // Act
            var bot = new TestActivityHandler(_storage.Object);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldCatchPreConditionFailedException()
        {
            // Arrange
            var adapter = new SharePointSSOAdapter(CaptureSend);
            adapter.AddExchangeableToken(ConnectionName, Channels.M365, UserId, FakeExchangeableItem, Token);

            var turnContext = adapter.CreateTurnContext(_activity);

            _storage.Setup(x => x.WriteAsync(It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>())).Throws(new Exception("Etag conflict: pre-condition is not met"));

            // Act
            var bot = new TestActivityHandler(_storage.Object);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldUseNativeV2CreateOnlySuccess()
        {
            // Arrange
            var adapter = new SharePointSSOAdapter(CaptureSend);
            adapter.AddExchangeableToken(ConnectionName, Channels.M365, UserId, FakeExchangeableItem, Token);

            var turnContext = adapter.CreateTurnContext(_activity);
            var storage = new NativeStorageStub(StorageOperationStatus.Succeeded);

            // Act
            var bot = new TestActivityHandler(storage);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.True(storage.V2WriteCalled);
            Assert.False(storage.V1WriteCalled);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldUseNativeV2CreateOnlyConflict()
        {
            // Arrange
            var adapter = new SharePointSSOAdapter(CaptureSend);
            adapter.AddExchangeableToken(ConnectionName, Channels.M365, UserId, FakeExchangeableItem, Token);

            var turnContext = adapter.CreateTurnContext(_activity);
            var storage = new NativeStorageStub(StorageOperationStatus.Conflict);

            // Act
            var bot = new TestActivityHandler(storage);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.True(storage.V2WriteCalled);
            Assert.False(storage.V1WriteCalled);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        [Fact]
        public async Task OnTurnAsync_ShouldCatchExceptionDuringExchange()
        {
            // Arrange
            var request = new { Data = ExceptionExpected, Properties = "Token", id = "test-id" };

            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/token",
                ChannelId = Channels.M365,
                From = new ChannelAccount(UserId, UserName),
                Conversation = new ConversationAccount(id: "test-conversation"),
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(request)
            };

            var adapter = new SharePointSSOAdapter(CaptureSend);
            adapter.AddExchangeableToken(ConnectionName, Channels.M365, UserId, ExceptionExpected, ExceptionExpected);

            var turnContext = new TurnContext(adapter, activity);

            // Act
            var bot = new TestActivityHandler(_storage.Object);
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSignInInvokeAsync", bot.Record[0]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
            Assert.Null(((InvokeResponse)_activitiesToSend[0].Value).Body);
        }

        private class TestActivityHandler(IStorage storage) : SharePointActivityHandler
        {
            private readonly IStorage _storage = storage;
            public List<string> Record { get; } = [];

            protected override Task OnSignInInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);

                var sso = new SharePointSSOTokenExchangeMiddleware(_storage, ConnectionName);
                return sso.OnTurnAsync(turnContext, cancellationToken);
            }
        }

        private class SharePointSSOAdapter(Action<IActivity[]> callOnSend) : TestAdapter
        {
            private readonly Action<IActivity[]> _callOnSend = callOnSend;

            public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
            {
                Assert.NotNull(activities);
                Assert.True(activities.Length > 0, "SharePointSSOAdapter.sendActivities: empty activities array.");

                _callOnSend?.Invoke(activities);
                List<ResourceResponse> responses = new List<ResourceResponse>();

                foreach (var activity in activities)
                {
                    responses.Add(new ResourceResponse(activity.Id));
                }

                return Task.FromResult(responses.ToArray());
            }
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
    }
}
