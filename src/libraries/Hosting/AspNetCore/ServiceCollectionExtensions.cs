// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        public static IHostApplicationBuilder AddAgent(this IHostApplicationBuilder builder, Func<IServiceProvider, IBot> implementationFactory, IStorage storage = null)
        {
            return AddAgent<CloudAdapter>(builder, implementationFactory, storage);
        }

        public static IHostApplicationBuilder AddAgent<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IBot> implementationFactory, IStorage storage = null)
            where TAdapter : CloudAdapter
        {
            AddCore<TAdapter>(builder, storage);

            builder.Services.AddTransient<IBot>(implementationFactory);

            return builder;
        }

        public static IHostApplicationBuilder AddAgent<TAgent>(this IHostApplicationBuilder builder, IStorage storage = null)
            where TAgent : class, IBot
        {
            return AddAgent<TAgent, CloudAdapter>(builder, storage);
        }

        public static IHostApplicationBuilder AddAgent<TAgent, TAdapter>(this IHostApplicationBuilder builder, IStorage storage = null)
            where TAgent : class, IBot
            where TAdapter : CloudAdapter
        {
            AddCore<TAdapter>(builder, storage);

            // Add the Agent 
            builder.Services.AddTransient<IBot, TAgent>();

            return builder;
        }

        /// <summary>
        /// Registers AgentApplicationOptions for AgentApplication-based Agents.
        /// </summary>
        /// <remarks>
        /// This loads options from IConfiguration and DI.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="fileDownloaders"></param>
        /// <param name="autoSignIn"></param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddAgentApplicationOptions(
            this IHostApplicationBuilder builder,
            IList<IInputFileDownloader> fileDownloaders = null,
            AutoSignInSelectorAsync autoSignIn = null)
        {
            if (autoSignIn != null)
            {
                builder.Services.AddSingleton<AutoSignInSelectorAsync>(sp => autoSignIn);
            }

            if (fileDownloaders != null)
            {
                builder.Services.AddSingleton(sp => fileDownloaders);
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
        /// Add the derived CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="async"></param>
        public static void AddCloudAdapter<T>(this IServiceCollection services) where T : CloudAdapter
        {
            AddAsyncCloudAdapterSupport(services);

            services.AddSingleton<CloudAdapter, T>();
            services.AddSingleton<IBotHttpAdapter>(sp => sp.GetService<CloudAdapter>());
            services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
        }



        private static void AddCore<TAdapter>(this IHostApplicationBuilder builder, IStorage storage = null)
            where TAdapter : CloudAdapter
        {
            // Add Connections object to access configured token connections.
            builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

            // Add factory for ConnectorClient and UserTokenClient creation
            builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

            // Add IStorage for turn state persistence
            if (storage != null)
            {
                builder.Services.AddSingleton(storage);
            }

            // Add the ChannelAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol.
            AddCloudAdapter<TAdapter>(builder.Services);
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
