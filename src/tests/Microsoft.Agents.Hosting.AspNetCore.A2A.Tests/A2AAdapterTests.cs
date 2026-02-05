// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A.Tests;

public class A2AAdapterTests
{
    private readonly Mock<ITaskStore> _mockTaskStore;
    private readonly Mock<ILogger<A2AAdapter>> _mockLogger;
    private readonly Mock<IStorage> _mockStorage;

    public A2AAdapterTests()
    {
        _mockTaskStore = new Mock<ITaskStore>();
        _mockLogger = new Mock<ILogger<A2AAdapter>>();
        _mockStorage = new Mock<IStorage>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithTaskStore_ShouldInitializeAdapter()
    {
        // Act
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter.OnTurnError);
    }

    [Fact]
    public void Constructor_WithStorage_ShouldInitializeAdapter()
    {
        // Act
        var adapter = new A2AAdapter(_mockStorage.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter.OnTurnError);
    }

    [Fact]
    public void Constructor_WithNullTaskStore_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new A2AAdapter((ITaskStore)null, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullStorage_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new A2AAdapter((IStorage)null, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithoutLogger_ShouldUseNullLogger()
    {
        // Act
        var adapter = new A2AAdapter(_mockTaskStore.Object);

        // Assert
        Assert.IsType<NullLogger<A2AAdapter>>(adapter.Logger);
    }

    [Fact]
    public async Task OnTurnError_ShouldLogErrorAndThrowA2AException()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger.Object);
        var activity = new Activity { Type = ActivityTypes.Message, Text = "test" };
        var context = new TurnContext(adapter, activity);
        var testException = new InvalidOperationException("Test exception");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<A2AException>(async () =>
        {
            await adapter.OnTurnError(context, testException);
        });

        Assert.Contains("An error occurred during turn processing", exception.Message);
        Assert.Equal(testException, exception.InnerException);
    }

    #endregion

    #region ProcessAgentCardAsync Tests

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldReturnDefaultAgentCard()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger.Object);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var mockAgent = new Mock<IAgent>();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithAgentAttribute_ShouldUseAttributeValues()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger.Object);
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
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger.Object);
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
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger.Object);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithSkills();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
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
            return Task.FromResult<InvokeResponse>(null);
        }
    }

    [Agent(name: "SkillAgent")]
    [A2ASkill(id: "skill1", name: "Test Skill", tags: "tag1;tag2", description: "A test skill")]
    private class TestAgentWithSkills : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<InvokeResponse>(null);
        }
    }

    private class TestAgentWithCardHandler : IAgent, IAgentCardHandler
    {
        public bool CardHandlerCalled { get; private set; }

        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<InvokeResponse>(null);
        }

        public Task<AgentCard> GetAgentCard(AgentCard defaultCard)
        {
            CardHandlerCalled = true;
            defaultCard.Name = "Custom Agent";
            return Task.FromResult(defaultCard);
        }
    }

    #endregion
}