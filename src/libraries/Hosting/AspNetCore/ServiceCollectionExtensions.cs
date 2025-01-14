// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
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

        public static IHostApplicationBuilder AddBot<TBot>(this IHostApplicationBuilder builder)
            where TBot : class, IBot
        {
            return AddBot<TBot, CloudAdapter>(builder);
        }

        public static IHostApplicationBuilder AddBot<TBot, TAdapter>(this IHostApplicationBuilder builder)
            where TBot : class, IBot
            where TAdapter : CloudAdapter
        {
            // Add Connections object to access configured token connections.
            builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

            // Add factory for ConnectorClient and UserTokenClient creation
            builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

            // Add the ChannelAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol.
            AddCloudAdapter<TAdapter>(builder.Services);

            // Add the Bot,  this is the primary worker for the bot. 
            builder.Services.AddTransient<IBot, TBot>();

            return builder;
        }

        public static IHostApplicationBuilder AddChannelHost<TService>(this IHostApplicationBuilder builder, Func<IServiceProvider, TService> responseHandlerFactory = null, string httpBotClientName = "HttpBotClient")
            where TService : class, IChannelResponseHandler
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(httpBotClientName);

            // Add the bots configuration class.  This loads client info and known bots.
            builder.Services.AddSingleton<IChannelHost, ConfigurationChannelHost>(
                (sp) =>
                new ConfigurationChannelHost(
                    sp.GetRequiredService<IServiceProvider>(),
                    sp.GetRequiredService<IConnections>(),
                    sp.GetRequiredService<IConfiguration>(),
                    defaultChannelName: httpBotClientName // this ensures that the default channel name is set to the http bot client name.
                                                          // Note: if this is overridden in the configuration, the value passed as httpBotClientName must also be updated.
                                                          //     It is not expected this will be needed for most customers. 
                    ));

            // Add bot client factory for HTTP
            // Use the same auth connection as the ChannelServiceFactory for now.
            builder.Services.AddKeyedSingleton<IChannelFactory>(httpBotClientName, (sp, key) => new HttpBotChannelFactory(
                sp.GetService<IHttpClientFactory>(),
                (ILogger<HttpBotChannelFactory>)sp.GetService(typeof(ILogger<HttpBotChannelFactory>))));

            // Add IStorage for turn state persistence
            builder.Services.AddSingleton<IStorage, MemoryStorage>();

            // Add conversation id factory.  
            // This is a memory only implementation, and for production would require persistence.
            builder.Services.AddSingleton<IConversationIdFactory, ConversationIdFactory>();

            // Add bot callback handler.
            // This is the object that handles callback endpoints for bot responses.
            if (responseHandlerFactory != null)
            {
                builder.Services.AddTransient(typeof(IChannelResponseHandler), responseHandlerFactory);
            }
            else
            {
                builder.Services.AddTransient<IChannelResponseHandler, TService>();
            }

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
