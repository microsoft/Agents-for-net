// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Extension methods for registering the named pipe transport
    /// with the Agents SDK host builder.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds named pipe transport for the agent, enabling pipe-based
        /// communication with DirectLineFlex without any HTTP roundtrips.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentNamedPipeTransport(this IHostApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            AddNamedPipeServices(builder);

            return builder;
        }

        /// <summary>
        /// Adds named pipe transport for the agent, enabling pipe-based
        /// communication with DirectLineFlex without any HTTP roundtrips.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="pipeName">
        /// The named pipe base name. When supplied, this explicitly overrides
        /// <c>NamedPipe:PipeName</c> from configuration.
        /// Two pipes are created: {pipeName}.incoming and {pipeName}.outgoing.
        /// </param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentNamedPipeTransport(
            this IHostApplicationBuilder builder,
            string pipeName)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

            builder.Configuration[$"NamedPipe:PipeName"] = pipeName;

            AddNamedPipeServices(builder);

            return builder;
        }

        private static void AddNamedPipeServices(IHostApplicationBuilder builder)
        {
            builder.Services.AddHttpClient();

            builder.Services.AddSingleton<NamedPipeActivityHandler>();
            builder.Services.AddSingleton<NamedPipeMessageHandler>();
            builder.Services.AddHostedService<NamedPipeHostedService>();

            builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                {
                    var pipeHandler = handlerBuilder.Services.GetRequiredService<NamedPipeMessageHandler>();
                    handlerBuilder.AdditionalHandlers.Add(new PipeRoutingDelegatingHandler(pipeHandler));
                });
            });
        }
    }

    /// <summary>
    /// <see cref="DelegatingHandler"/> that routes urn:botframework:namedpipe requests to the
    /// <see cref="NamedPipeMessageHandler"/>, while passing all other requests through normally.
    /// </summary>
    internal sealed class PipeRoutingDelegatingHandler(NamedPipeMessageHandler pipeHandler) : DelegatingHandler
    {
        private readonly NamedPipeMessageHandler _pipeHandler = pipeHandler ?? throw new ArgumentNullException(nameof(pipeHandler));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (PipeUriPredicate.IsNamedPipeUri(request.RequestUri))
            {
                return _pipeHandler.SendViaPipeAsync(request, cancellationToken);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
