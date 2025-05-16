﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;

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

            // Add the Agent 
            builder.Services.AddTransient<IAgent, TAgent>();

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
            AddAsyncCloudAdapterSupport(services);

            services.AddSingleton<CloudAdapter, T>();
            services.AddSingleton<IAgentHttpAdapter>(sp => sp.GetService<CloudAdapter>());
            services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
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

        private static void AddAsyncCloudAdapterSupport(this IServiceCollection services)
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
}
