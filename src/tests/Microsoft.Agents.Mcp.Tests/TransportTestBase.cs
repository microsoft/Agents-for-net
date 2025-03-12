using Microsoft.Agents.Mcp.Client.DependencyInjection;
using Microsoft.Agents.Mcp.Client.Initialization;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.DependencyInjection;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Agents.Mcp.Server.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Mcp.Tests
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
