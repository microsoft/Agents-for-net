// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AAdapterTests
{
    private readonly Mock<ITaskStore> _mockTaskStore;
    private readonly ILoggerFactory _mockLogger;
    private readonly Mock<IStorage> _mockStorage;

    public A2AAdapterTests()
    {
        _mockTaskStore = new Mock<ITaskStore>();
        _mockStorage = new Mock<IStorage>();
        _mockLogger = NullLoggerFactory.Instance;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithTaskStore_ShouldInitializeAdapter()
    {
        // Act
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter.OnTurnError);
    }

    [Fact]
    public void Constructor_WithStorage_ShouldInitializeAdapter()
    {
        // Act
        var adapter = new A2AAdapter(_mockStorage.Object, _mockLogger);

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter.OnTurnError);
    }

    [Fact]
    public void Constructor_WithNullTaskStore_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new A2AAdapter((ITaskStore)null, _mockLogger));
    }

    #endregion

    #region ProcessAgentCardAsync Tests

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldReturnDefaultAgentCard()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var mockAgent = new Mock<IAgent>();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
        mockHttpResponse.Object.Body.Position = 0;
        var agentCard = await JsonSerializer.DeserializeAsync<AgentCard>(
            mockHttpResponse.Object.Body, A2AJsonUtilities.DefaultOptions);
        Assert.False(agentCard!.Capabilities.ExtendedAgentCard);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldIncludeCacheControlMaxAge()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var mockAgent = new Mock<IAgent>();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);

        // Assert
        Assert.Equal("public, max-age=3600", mockHttpResponse.Object.Headers.CacheControl);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithOptions_ShouldUseConfiguredCacheMaxAge()
    {
        // Arrange
        var adapter = new A2AAdapter(
            _mockTaskStore.Object,
            _mockLogger,
            options: new A2AAdapterOptions { AgentCardCacheMaxAge = TimeSpan.FromMinutes(15) });
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);

        // Assert
        Assert.Equal("public, max-age=900", mockHttpResponse.Object.Headers.CacheControl);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithConfiguration_ShouldUseConfiguredCacheMaxAge()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["A2AAdapterOptions:AgentCardCacheMaxAge"] = "00:10:00",
            })
            .Build();
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger, configuration: configuration);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);

        // Assert
        Assert.Equal("public, max-age=600", mockHttpResponse.Object.Headers.CacheControl);
    }

    [Fact]
    public void Constructor_WithNegativeAgentCardCacheMaxAge_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new A2AAdapter(
            _mockTaskStore.Object,
            _mockLogger,
            options: new A2AAdapterOptions { AgentCardCacheMaxAge = TimeSpan.FromSeconds(-1) }));
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldIncludeBodyDerivedETag()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var defaultResponse = CreateMockHttpResponse();
        var attributedResponse = CreateMockHttpResponse();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, defaultResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, attributedResponse.Object, new TestAgentWithAttributes(), "/a2a", CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrEmpty(defaultResponse.Object.Headers.ETag));
        Assert.NotEqual(defaultResponse.Object.Headers.ETag, attributedResponse.Object.Headers.ETag);
        Assert.Equal(
            $"\"{Convert.ToHexString(SHA256.HashData(((MemoryStream)defaultResponse.Object.Body).ToArray()))}\"",
            defaultResponse.Object.Headers.ETag);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldIncludeStableLastModified()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var firstResponse = CreateMockHttpResponse();
        var secondResponse = CreateMockHttpResponse();
        var mockAgent = new Mock<IAgent>();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, firstResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, secondResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);

        // Assert
        Assert.Equal(firstResponse.Object.Headers.LastModified, secondResponse.Object.Headers.LastModified);
        Assert.True(DateTimeOffset.TryParseExact(
            firstResponse.Object.Headers.LastModified,
            "R",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out _));
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithAgentAttribute_ShouldUseAttributeValues()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithAttributes();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithAgentCardHandler_ShouldCallHandler()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithCardHandler();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        Assert.True(agent.CardHandlerCalled);
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithSkills_ShouldIncludeSkills()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithSkills();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    #endregion

    #region

    [Fact]
    public async Task ProcessJsonRpcMessageSendAsync()
    {
        // Arrange
        var record = UseRecord((record) =>
        {
            var options = new TestApplicationOptions(record.Storage);
            var agent = new TestApplication(options);

            agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
            {
                await context.SendActivityAsync($"Echo: {context.Activity.Text}", cancellationToken: ct);
            });

            return agent;
        });

        var jsonRpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.SendMessage,
            Params = JsonSerializer.SerializeToElement(new SendMessageRequest
            {
                Message = new Message
                {
                    ContextId = "context-1234",
                    Parts = [new Part() { Text = "Hello" }]
                }
            })
        };

        var context = CreateHttpContext(JsonSerializer.Serialize(jsonRpcRequest));

        // Act

        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        // Assert

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var streamText = reader.ReadToEnd();
        var jsonRpcResponse = ProtocolJsonSerializer.ToObject<JsonRpcResponse>(streamText);
        Assert.NotNull(jsonRpcResponse);
        var task = ProtocolJsonSerializer.ToObject<AgentTask>(jsonRpcResponse.Result.AsObject().GetAt(0).Value);
        Assert.NotNull(task);
        Assert.Equal("context-1234", task.ContextId);
        Assert.NotEmpty(task.Id);
        Assert.NotNull(task.Status.Message);
        Assert.Equal("Echo: Hello", task.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ProcessJsonRpcMessageStreamAsync()
    {
        // Arrange
        var record = UseRecord((record) =>
        {
            var options = new TestApplicationOptions(record.Storage);
            var agent = new TestApplication(options);

            agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
            {
                await context.SendActivityAsync($"Echo: {context.Activity.Text}", cancellationToken: ct);
            });

            return agent;
        });

        var jsonRpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.SendStreamingMessage,
            Params = JsonSerializer.SerializeToElement(new SendMessageRequest
            {
                Message = new Message
                {
                    ContextId = "context-1234",
                    Parts = [new Part() { Text = "Hello" }]
                },
                Configuration = new SendMessageConfiguration
                {
                    HistoryLength = 10,
                }
            })
        };

        var context = CreateHttpContext(JsonSerializer.Serialize(jsonRpcRequest));

        // Act

        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        // Assert

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var streamText = reader.ReadToEnd();
        Assert.NotEmpty(streamText);
        Assert.Contains("Echo: Hello", streamText);
    }

    [Fact]
    public async Task ProcessJsonRpcGetExtendedAgentCardAsync_WhenUnsupported_ReturnsUnsupportedOperation()
    {
        // Arrange
        var record = UseRecord(record =>
        {
            var options = new TestApplicationOptions(record.Storage);
            return new TestApplication(options);
        });

        var jsonRpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.GetExtendedAgentCard,
            Params = JsonSerializer.SerializeToElement(new GetExtendedAgentCardRequest())
        };

        var context = CreateHttpContext(JsonSerializer.Serialize(jsonRpcRequest));

        // Act
        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal((int)A2AErrorCode.UnsupportedOperation, response.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    #endregion

    #region Helper Methods

    private Mock<HttpRequest> CreateMockHttpRequest(string scheme = "https", string host = "localhost:3978")
    {
        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(r => r.Scheme).Returns(scheme);
        mockRequest.Setup(r => r.Host).Returns(new HostString(host));
        mockRequest.Setup(r => r.Headers).Returns(new HeaderDictionary());
        mockRequest.Setup(r => r.Body).Returns(new MemoryStream());
        return mockRequest;
    }

    private Mock<HttpResponse> CreateMockHttpResponse()
    {
        var mockResponse = new Mock<HttpResponse>();
        var memoryStream = new MemoryStream();
        mockResponse.SetupProperty(r => r.ContentType);
        mockResponse.Setup(r => r.Headers).Returns(new HeaderDictionary());
        mockResponse.Setup(r => r.Body).Returns(memoryStream);
        return mockResponse;
    }

    #endregion

    #region Test Helper Classes

    [Agent(name: "TestAgent", description: "A test agent", version: "1.0.0")]
    private class TestAgentWithAttributes : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Agent(name: "SkillAgent")]
    [A2ASkill(id: "skill1", name: "Test Skill", tags: "tag1;tag2", description: "A test skill")]
    private class TestAgentWithSkills : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestAgentWithCardHandler : IAgent, IAgentCardHandler
    {
        public bool CardHandlerCalled { get; private set; }

        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<AgentCard> GetAgentCard(AgentCard defaultCard)
        {
            CardHandlerCalled = true;
            defaultCard.Name = "Custom Agent";
            return Task.FromResult(defaultCard);
        }
    }

    #endregion

    private static DefaultHttpContext CreateHttpContext(string requestContent = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
        context.Request.Method = HttpMethods.Post;
        context.Response.StatusCode = 0;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Record UseRecord(Func<Record, IAgent> createAgent)
    {
        var storage = new MemoryStorage();
        var adapter = new A2AAdapter(storage, NullLoggerFactory.Instance);
        var record = new Record(storage, adapter, null);

        if (createAgent != null)
        {
            record.Agent = createAgent(record);
        }

        return record;
    }

    private record Record(
        IStorage Storage,
        A2AAdapter Adapter,
        IAgent Agent)
    {

        public IAgent Agent { get; set; } = Agent;
        public IStorage Storage { get; set; } = Storage;
    }

}