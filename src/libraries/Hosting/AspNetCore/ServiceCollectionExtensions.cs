// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Provides extension methods for registering agent-related services, adapters, and middleware with dependency
    /// injection containers and application builders.
    /// </summary>
    /// <remarks>These extension methods simplify the setup of agent applications by enabling the registration
    /// of agents, adapters, options, and supporting middleware. They are intended to be used during application startup
    /// to configure required services for agent-based architectures, such as those using CloudAdapter and
    /// AgentApplication. Methods in this class support both default and custom agent/adapters, and facilitate
    /// integration with ASP.NET Core's dependency injection and middleware pipelines.</remarks>
    public static class ServiceCollectionExtensions
    {
        #region Builder Extensions
        /// <summary>
        /// Adds an Agent which subclasses <c>AgentApplication</c>
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgent&lt;MyAgent>();
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also call <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgentCore(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient. <see cref="AgentApplicationOptions"/> is automatically registered
        /// if not already present.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent<TAgent>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
        {
            return builder.AddAgent<TAgent, CloudAdapter>();
        }

        /// <summary>
        /// Same as <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgent{TAgent}(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> but allows for use of
        /// any <c>CloudAdapter</c> subclass.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgent&lt;MyAgent, MyCustomAdapter&gt;();
        /// </code>
        /// </summary>
        /// <remarks>
        /// <see cref="AgentApplicationOptions"/> is automatically registered if not already present.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent<TAgent, TAdapter>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
            where TAdapter : CloudAdapter
        {
            builder.Services.AddAgent<TAgent, TAdapter>();
            return builder;
        }

        /// <summary>
        /// Adds an Agent via lambda construction.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
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
        /// This will also call <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgentCore(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient. <see cref="AgentApplicationOptions"/> is automatically registered
        /// if not already present.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
        {
            return builder.AddAgent<CloudAdapter>(implementationFactory);
        }

        /// <summary>
        /// This is the same as <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgent(Microsoft.Extensions.Hosting.IHostApplicationBuilder, System.Func{System.IServiceProvider, Microsoft.Agents.Builder.IAgent})"/>, except allows the
        /// use of any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <remarks>
        /// <see cref="AgentApplicationOptions"/> is automatically registered if not already present.
        /// </remarks>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
            where TAdapter : CloudAdapter
        {
            builder.Services.AddAgent<TAdapter>(implementationFactory);
            return builder;
        }

        /// <summary>
        /// Add the default CloudAdapter.
        /// </summary>
        /// <param name="builder">The host application builder to which the cloud adapter services will be added. Cannot be null.</param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddCloudAdapter(this IHostApplicationBuilder builder)
        {
            builder.Services.AddCloudAdapter();
            return builder;
        }

        /// <summary>
        /// Add a derived CloudAdapter.
        /// </summary>
        /// <param name="builder">The host application builder to which the cloud adapter services will be added. Cannot be null.</param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddCloudAdapter<T>(this IHostApplicationBuilder builder) where T : CloudAdapter
        {
            builder.Services.AddCloudAdapter<T>();
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
        /// <param name="autoSignIn"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentApplicationOptions(this IHostApplicationBuilder builder, AutoSignInSelector autoSignIn = null)
        {
            builder.Services.AddAgentApplicationOptions(autoSignIn);
            return builder;
        }

        /// <summary>
        /// Adds the core agent services.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentCore(this IHostApplicationBuilder builder)
        {
            return builder.AddAgentCore<CloudAdapter>();
        }

        /// <summary>
        /// Adds the core agent services using a derived CloudAdapter.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentCore<TAdapter>(this IHostApplicationBuilder builder) where TAdapter : CloudAdapter
        {
            builder.Services.AddAgentCore<TAdapter>();
            return builder;
        }

        /// <summary>
        /// Adds a middleware that collects headers to be propagated.
        /// </summary>
        /// <param name="app">The <see cref="Microsoft.AspNetCore.Builder.IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);
            return app.UseMiddleware<HeaderPropagationMiddleware>();
        }
        #endregion

        #region IServiceCollection Extensions
        /// <summary>
        /// Adds the required agent application options and, optionally, an auto sign-in selector to the service
        /// collection.
        /// </summary>
        /// <param name="services">The service collection to which the agent application options and auto sign-in selector are added.</param>
        /// <param name="autoSignIn">An optional delegate used to select the auto sign-in behavior. If provided, it is registered as a singleton
        /// service.</param>
        /// <param name="replaceExisting">If true, replaces existing registrations of the agent application options and auto sign-in selector.</param>
        public static IServiceCollection AddAgentApplicationOptions(this IServiceCollection services, AutoSignInSelector autoSignIn = null, bool replaceExisting = true)
        {
            if (autoSignIn != null)
            {
                if (replaceExisting || !services.Any(x => x.ServiceType == typeof(AutoSignInSelector)))
                {
                    services.AddSingleton<AutoSignInSelector>(sp => autoSignIn);
                }
            }

            if (replaceExisting || !services.Any(x => x.ServiceType == typeof(AgentApplicationOptions)))
            {
                services.AddSingleton<AgentApplicationOptions>();
            }
            return services;
        }

        /// <summary>
        /// Adds an agent and its associated cloud adapter to the service collection for dependency injection.
        /// <code>
        /// services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// services.AddAgent&lt;MyAgent, CloudAdapter&gt;();
        /// </code>
        /// </summary>
        /// <remarks>Registers both the agent and its adapter as transient services. Only one instance of
        /// each agent type is registered. <see cref="AgentApplicationOptions"/> is automatically registered
        /// if not already present. This method is typically used to configure multi-agent scenarios in
        /// applications that use dependency injection.</remarks>
        /// <typeparam name="TAgent">The type of the agent to register. Must implement the IAgent interface.</typeparam>
        /// <typeparam name="TAdapter">The type of the cloud adapter to register. Must derive from CloudAdapter.</typeparam>
        /// <param name="services">The service collection to which the agent and adapter will be added.</param>
        public static IServiceCollection AddAgent<TAgent, TAdapter>(this IServiceCollection services)
            where TAgent : class, IAgent
            where TAdapter : CloudAdapter
        {
            if (typeof(AgentApplication).IsAssignableFrom(typeof(TAgent)))
            {
                services.AddAgentApplicationOptions(replaceExisting: false);
            }

            services.AddAgentCore<TAdapter>();

            // Add the IAgent 
            if (!services.Any(x => x.ServiceType == typeof(IAgent)))
            {
                // There can only be one IAgent.
                services.AddTransient<IAgent, TAgent>();
            }

            // Add the TAgent (required for multi agent registrations)
            if (!services.Any(x => x.ServiceType == typeof(TAgent)))
            {
                // There can only be one TAgent.
                services.AddTransient<TAgent>();
            }
            return services;
        }

        /// <summary>
        /// Adds an agent and its associated adapter to the service collection using the specified implementation
        /// factory.
        /// </summary>
        /// <remarks>This method registers the specified agent and its adapter for dependency injection.
        /// The agent is registered with a transient lifetime. Call this method during application startup to enable
        /// agent-based functionality.</remarks>
        /// <typeparam name="TAdapter">The type of the cloud adapter to associate with the agent. Must inherit from CloudAdapter.</typeparam>
        /// <param name="services">The service collection to which the agent and adapter are added.</param>
        /// <param name="implementationFactory">A factory function that creates an instance of IAgent using the provided service provider.</param>
        public static IServiceCollection AddAgent<TAdapter>(this IServiceCollection services, Func<IServiceProvider, IAgent> implementationFactory) where TAdapter : CloudAdapter
        {
            services.AddAgentApplicationOptions(replaceExisting: false);
            services.AddAgentCore<TAdapter>();
            services.AddTransient<IAgent>(implementationFactory);
            return services;
        }

        /// <summary>
        /// Adds core services required for Agent functionality, including the specified cloud adapter, to
        /// the application's dependency injection container.
        /// </summary>
        /// <remarks>This method registers essential services such as IConnections and
        /// IChannelServiceClientFactory if they are not already present. It also adds the specified CloudAdapter
        /// implementation, enabling integration with Azure Bot Service and Activity Protocol Agents.</remarks>
        /// <typeparam name="TAdapter">The type of cloud adapter to register. Must inherit from CloudAdapter.</typeparam>
        /// <param name="services">The service collection to which the agent core services will be added.</param>
        public static IServiceCollection AddAgentCore<TAdapter>(this IServiceCollection services) where TAdapter : CloudAdapter
        {
            if (!services.Any(x => x.ServiceType == typeof(IConnections)))
            {
                // Add Connections object to access configured token connections.
                services.AddSingleton<IConnections, ConfigurationConnections>();
            }

            if (!services.Any(x => x.ServiceType == typeof(IChannelServiceClientFactory)))
            {
                // Add factory for ConnectorClient and UserTokenClient creation
                services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();
            }

            // Add the CloudAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.
            services.AddCloudAdapter<TAdapter>();
            return services;
        }

        /// <summary>
        /// Add the default CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddCloudAdapter(this IServiceCollection services)
        {
            services.AddCloudAdapter<CloudAdapter>();
            return services;
        }

        /// <summary>
        /// Add a derived CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddCloudAdapter<T>(this IServiceCollection services) where T : CloudAdapter
        {
            AddAsyncAdapterSupport(services);

            if (!services.Any(x => x.ServiceType == typeof(T)))
            {
                services.AddSingleton<CloudAdapter, T>();
                services.AddSingleton<IAgentHttpAdapter>(sp => sp.GetService<CloudAdapter>());
                services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
            }
            return services;
        }

        /// <summary>
        /// Adds background task and activity processing support to the specified service collection, enabling
        /// asynchronous task execution via hosted services and task queues.
        /// </summary>
        /// <remarks>This method registers hosted services and singleton task queues required for
        /// background and activity processing. It is safe to call multiple times; services are only added if not
        /// already present. Use this method to enable asynchronous task and activity handling in applications that
        /// require background processing.</remarks>
        /// <param name="services">The service collection to which the background task and activity processing services will be added. Cannot
        /// be null.</param>
        public static IServiceCollection AddAsyncAdapterSupport(this IServiceCollection services)
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
            return services;
        }
        #endregion
    }
}