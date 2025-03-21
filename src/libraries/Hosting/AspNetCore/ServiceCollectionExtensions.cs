// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.Client;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Hosting.AspNetCore.HeaderPropagation;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
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
            AddHeaderPropagation(builder);
            AddHttpClientFactory(builder);
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
        /// Adds bot-to-bot functionality.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="builder"></param>
        /// <param name="httpBotClientName"></param>
        /// <param name="storage">Used for ConversationIdFactory.  If null, the registered IStorage will be used.</param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddChannelHost<THandler>(this IHostApplicationBuilder builder, string httpBotClientName = "HttpBotClient", IStorage storage = null)
            where THandler : class, IChannelApiHandler
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

            // Add conversation id factory.  
            // This is a memory only implementation, and for production would require persistence.
            if (storage != null)
            {
                builder.Services.AddSingleton<IConversationIdFactory>(sp => new ConversationIdFactory(storage));
            }

            // Add bot callback handler.
            // This is the object that handles callback endpoints for bot responses.
            builder.Services.AddTransient<THandler>();
            builder.Services.AddTransient<IChannelApiHandler>((sp) => sp.GetService<THandler>());

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

        /// <summary>
        /// Adds a middleware that collects headers to be propagated to an <see cref="IHttpClientFactory"/> instance.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <param name="configureOptions">The <see cref="HeaderPropagationOptions"/> configuration to select which headers to propagate from incoming to outgoing requests.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder app, Action<HeaderPropagationOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(configureOptions);

            configureOptions(app.ApplicationServices.GetRequiredService<HeaderPropagationOptions>());

            return app.UseMiddleware<HeaderPropagationMiddleware>();
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

        /// <summary>
        /// Adds a custom Http Client factory that wraps the default <see cref="IHttpClientFactory"/>.
        /// </summary>
        private static void AddHttpClientFactory(this IHostApplicationBuilder builder)
        {
            var defaultDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IHttpClientFactory));
            if (defaultDescriptor == null)
            {
                builder.Services.AddSingleton<IHttpClientFactory>(sp => new AgentsHttpClientFactory(sp));
                return;
            }

            // Remove the default registration.
            builder.Services.Remove(defaultDescriptor);

            // Capture a factory delegate that can create the default IHttpClientFactory instance.
            Func<IServiceProvider, IHttpClientFactory> defaultFactory;
            if (defaultDescriptor.ImplementationFactory != null)
            {
                defaultFactory = provider => (IHttpClientFactory)defaultDescriptor.ImplementationFactory(provider);
            }
            else if (defaultDescriptor.ImplementationInstance != null)
            {
                defaultFactory = _ => (IHttpClientFactory)defaultDescriptor.ImplementationInstance;
            }
            else
            {
                defaultFactory = provider => ActivatorUtilities.CreateInstance(provider, defaultDescriptor.ImplementationType) as IHttpClientFactory;
            }

            // Register custom factory that wraps the default.
            builder.Services.AddSingleton<IHttpClientFactory>(sp => new AgentsHttpClientFactory(sp, defaultFactory(sp)));
        }

        /// <summary>
        /// Adds header propagation services.
        /// </summary>
        private static void AddHeaderPropagation(this IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton<HeaderPropagationOptions>();
            builder.Services.AddSingleton<HeaderPropagationContext>();
        }
    }
}
