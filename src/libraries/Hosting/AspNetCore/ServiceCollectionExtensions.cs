// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds an Agent which subclasses <c>AgentApplication</c>
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgentApplicationOptions();
        /// builder.AddAgent&lt;MyAgent&gt;();
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also call <see cref="AddAgentCore(IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgent<TAgent>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
        {
            return AddAgent<TAgent, CloudAdapter>(builder);
        }

        /// <summary>
        /// Same as <see cref="AddAgent{TAgent}(IHostApplicationBuilder)"/> but allows for use of
        /// any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <typeparam name="TAgent"></typeparam>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgent<TAgent, TAdapter>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
            where TAdapter : CloudAdapter
        {
            AddAgentCore<TAdapter>(builder);

            // Add the IAgent 
            if (!builder.Services.Any(x => x.ServiceType == typeof(IAgent)))
            {
                // There can only be one IAgent.
                builder.Services.AddTransient<IAgent, TAgent>();
            }

            // Add the TAgent (required for multi agent registrations)
            if (!builder.Services.Any(x => x.ServiceType == typeof(TAgent)))
            {
                // There can only be one TAgent.
                builder.Services.AddTransient<TAgent>();
            }

            return builder;
        }

        /// <summary>
        /// Adds an Agent via lambda construction.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgentApplicationOptions();
        /// builder.AddAgent(sp =>
        /// {
        ///    var options = new AgentApplicationOptions()
        ///    {
        ///       TurnStateFactory = () => new TurnState(sp.GetService&lt;IStorage&gt;());
        ///    };
        ///        
        ///    var app = new AgentApplication(options);
        ///
        ///    ...
        ///
        ///    return app;
        /// });
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also calls <see cref="AddAgentCore(IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        public static IHostApplicationBuilder AddAgent(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
        {
            return AddAgent<CloudAdapter>(builder, implementationFactory);
        }

        /// <summary>
        /// This is the same as <see cref="AddAgent(IHostApplicationBuilder, Func{IServiceProvider, IAgent})"/>, except allows the
        /// use of any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        public static IHostApplicationBuilder AddAgent<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
            where TAdapter : CloudAdapter
        {
            AddAgentCore<TAdapter>(builder);

            builder.Services.AddTransient<IAgent>(implementationFactory);

            return builder;
        }

        /// <summary>
        /// Registers AgentApplicationOptions for AgentApplication-based Agents.
        /// </summary>
        /// <remarks>
        /// This loads options from IConfiguration and DI.  The <c>AgentApplicationOptions</c> is
        /// added as a singleton.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="fileDownloaders"></param>
        /// <param name="autoSignIn"></param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddAgentApplicationOptions(
            this IHostApplicationBuilder builder,
            AutoSignInSelector autoSignIn = null)
        {
            if (autoSignIn != null)
            {
                builder.Services.AddSingleton<AutoSignInSelector>(sp => autoSignIn);
            }

            builder.Services.AddSingleton<AgentApplicationOptions>();

            return builder;
        }

        /// <summary>
        /// Add the default CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="async"></param>
        public static void AddCloudAdapter(this IServiceCollection services)
        {
            services.AddCloudAdapter<CloudAdapter>();
        }

        /// <summary>
        /// Add a derived CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="async"></param>
        public static void AddCloudAdapter<T>(this IServiceCollection services) where T : CloudAdapter
        {
            AddAsyncAdapterSupport(services);

            services.AddSingleton<CloudAdapter, T>();
            services.AddSingleton<IAgentHttpAdapter>(sp => sp.GetService<CloudAdapter>());
            services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
        }

        /// <summary>
        /// Adds a middleware that collects headers to be propagated.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<HeaderPropagationMiddleware>();
        }

        /// <summary>
        /// Adds the core agent services.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.  Needed for Azure Bot Service and Agent-to-Agent.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgentCore(this IHostApplicationBuilder builder)
        {
            return builder.AddAgentCore<CloudAdapter>();
        }

        public static IHostApplicationBuilder AddAgentCore<TAdapter>(this IHostApplicationBuilder builder)
            where TAdapter : CloudAdapter
        {
            // Add Connections object to access configured token connections.
            builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

            // Add factory for ConnectorClient and UserTokenClient creation
            builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

            // Add the CloudAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.
            AddCloudAdapter<TAdapter>(builder.Services);

            return builder;
        }

        public static void AddAsyncAdapterSupport(this IServiceCollection services)
        {
            if (!services.Any(x => x.ServiceType == typeof(IActivityTaskQueue)))
            {
                // Activity specific BackgroundService for processing authenticated activities.
                services.AddHostedService<HostedActivityService>();
                // Generic BackgroundService for processing tasks.
                services.AddHostedService<HostedTaskService>();

                // BackgroundTaskQueue and ActivityTaskQueue are the entry points for
                // the enqueueing activities or tasks to be processed by the BackgroundService.
                services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                services.AddSingleton<IActivityTaskQueue, ActivityTaskQueue>();
            }
        }

        public delegate Task ProcessRequestDelegate<TAdapter, TAgent>(HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter;

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints(
            this WebApplication app, 
            bool requireAuth = true, 
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, IAgent> process = null)
        {
            return app.MapAgentEndpoints<IAgentHttpAdapter, IAgent>(requireAuth, path, process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints<TAgent>(
            this WebApplication app, 
            bool requireAuth = true, 
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, TAgent> process = null)
            where TAgent : IAgent
        {
            return app.MapAgentEndpoints<IAgentHttpAdapter, TAgent>(requireAuth, path, process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints<TAdapter, TAgent>(
            this WebApplication app, 
            bool requireAuth = true, 
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<TAdapter, TAgent> process = null) 
            where TAgent : IAgent 
            where TAdapter : IAgentHttpAdapter
        {
            var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            app.MapGet("/", () => $"Microsoft Agents SDK: {assemblyName}, version {assemblyVersion}");

            var agentGroup = app.MapGroup("");
            if (requireAuth)
            {
                app.UseAuthentication();
                app.UseAuthorization();

                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            // This receives incoming messages from Azure Bot Service or other SDK Agents
            agentGroup.MapPost(path, async (HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken) =>
            {
                if (process != null)
                {
                    await process(request, response, adapter, agent, cancellationToken);
                }
                else
                {
                    await adapter.ProcessAsync(request, response, agent, cancellationToken);
                }
            });

            return agentGroup;
        }
    }
}
