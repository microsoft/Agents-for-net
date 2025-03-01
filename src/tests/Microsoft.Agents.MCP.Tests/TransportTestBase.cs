using Microsoft.Agents.MCP.Client.DependencyInjection;
using Microsoft.Agents.MCP.Client.Initialization;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.DependencyInjection;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Server.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.MCP.Tests
{
    public abstract class TransportTestBase
    {
        [Fact]
        public async Task RoundTripTransport()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddModelContextProtocolHandlers();
            serviceCollection.AddDefaultOperationFactory();
            serviceCollection.AddDefaultPayloadExecutionFactory();
            serviceCollection.AddDefaultPayloadResolver();
            serviceCollection.AddMemorySessionManager();
            serviceCollection.AddTransportManager();

            serviceCollection.AddDefaultServerExecutors();
            serviceCollection.AddDefaultClientExecutors();

            using var services = serviceCollection.BuildServiceProvider();
            var processor = services.GetRequiredService<IMcpProcessor>();
            var transportManager = services.GetRequiredService<ITransportManager>();
            var logger = services.GetRequiredService<ILogger<SseTransportTests>>();

            IMcpTransport transport = CreateTransport(processor, transportManager, logger);

            var session = await processor.CreateSessionAsync(transport, CancellationToken.None);
            await ClientRequestHelpers.InitializeAsync(session, new InitializationParameters() { }, CancellationToken.None);
            var ping = await ClientRequestHelpers.SendAsync<PingResponse>(session, new McpPingRequest(PingRequestParameters.Instance), CancellationToken.None);

        }

        protected abstract IMcpTransport CreateTransport(IMcpProcessor processor, ITransportManager transportManager, ILogger<SseTransportTests> logger);
    }
}
