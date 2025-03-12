using Microsoft.Agents.Mcp.Client.Transports;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Mcp.Tests
{
    public class StdioTransportTests : TransportTestBase
    {
        private readonly string command = "node";
        private readonly string[] arguments = { "--port", "stdio" };

        protected override IMcpTransport CreateTransport(IMcpProcessor processor, ITransportManager transportManager, ILogger<TransportTestBase> logger)
        {
            return new StdioClientTransport(command, arguments);
        }

        [Fact]
        public void CreateTransport_ShouldReturn_StdioClientTransport()
        {
            // Arrange
            var processorMock = new Mock<IMcpProcessor>();
            var transportManagerMock = new Mock<ITransportManager>();
            var loggerMock = new Mock<ILogger<StdioTransportTests>>();

            // Act
            var transport = CreateTransport(processorMock.Object, transportManagerMock.Object, loggerMock.Object);

            // Assert
            Assert.IsType<StdioClientTransport>(transport);
        }

        [Fact]
        public void CreateTransport_ShouldNotSetIsClosed()
        {
            // Arrange
            var processorMock = new Mock<IMcpProcessor>();
            var transportManagerMock = new Mock<ITransportManager>();
            var loggerMock = new Mock<ILogger<StdioTransportTests>>();

            // Act
            var transport = CreateTransport(processorMock.Object, transportManagerMock.Object, loggerMock.Object);

            // Assert
            Assert.False(transport.IsClosed);
        }

        [Fact]
        public async Task ProcessPayloadAsync_ShouldInvokeIngestionFunc()
        {
            // Arrange
            var processorMock = new Mock<IMcpProcessor>();
            var transportManagerMock = new Mock<ITransportManager>();
            var loggerMock = new Mock<ILogger<StdioTransportTests>>();
            var mockIngestFunc = new Mock<Func<JsonRpcPayload, CancellationToken, Task>>();
            var mockCloseFunc = new Mock<Func<CancellationToken, Task>>();

            // Act
            var transport = CreateTransport(processorMock.Object, transportManagerMock.Object, loggerMock.Object);
            await transport.Connect("sessionId", mockIngestFunc.Object, mockCloseFunc.Object);

            // Setup the mock to verify the ingestion function is called
            var payload = new JsonRpcPayload { Method = "testMethod", Params = JsonSerializer.SerializeToElement(new { param1 = "value1" }) };


            // Act
            await transport.ProcessPayloadAsync(payload, CancellationToken.None);

            // Assert
            mockIngestFunc.Verify(func => func(payload, CancellationToken.None), Times.Once);
        }

        [Fact]
        public void CloseAsync_ShouldSetIsClosed()
        {
            // Arrange
            var processorMock = new Mock<IMcpProcessor>();
            var transportManagerMock = new Mock<ITransportManager>();
            var loggerMock = new Mock<ILogger<StdioTransportTests>>();

            // Act
            var transport = CreateTransport(processorMock.Object, transportManagerMock.Object, loggerMock.Object);
            transport.CloseAsync(CancellationToken.None);

            Assert.True(transport.IsClosed);
        }
    }
}