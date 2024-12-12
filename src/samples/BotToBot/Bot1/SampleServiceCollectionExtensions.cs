// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Protocols.Connector;
using Microsoft.Agents.Client;
using Microsoft.Agents.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Agents.Samples.Bots;

namespace Microsoft.Agents.Hosting.Setup
{
    public static class SampleServiceCollectionExtensions
    {
        public static IHostApplicationBuilder AddBot<T, TImpl>(this IHostApplicationBuilder builder)
            where T : IBot
            where TImpl : class, T
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddBotAspNetAuthentication(builder.Configuration);

            // Add Connections object to access configured token connections.
            builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

            // Add factory for ConnectorClient and UserTokenClient creation
            builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

            // Add the BotAdapter
            builder.Services.AddCloudAdapter<BotHostAdapterWithErrorHandler>();

            // Add the Bot
            builder.Services.AddTransient<Bot1>();
            builder.Services.AddTransient<IBot>((sp) => sp.GetService<Bot1>());

            //
            // ChannelHost
            //

            // Add the bots configuration class.  This loads client info and known bots.
            builder.Services.AddSingleton<IChannelHost, ConfigurationChannelHost>();

            // Add bot client factory for HTTP
            // Use the same auth connection as the ChannelServiceFactory for now.
            builder.Services.AddKeyedSingleton<IChannelFactory>("HttpBotClient", (sp, key) => new HttpBotChannelFactory(
                sp.GetService<IHttpClientFactory>(),
                (ILogger<HttpBotChannelFactory>)sp.GetService(typeof(ILogger<HttpBotChannelFactory>))));

            // Add IStorage for turn state persistence
            builder.Services.AddSingleton<IStorage, MemoryStorage>();

            // Add conversation id factory.  
            // This is a memory only implementation, and for production would require persistence.
            builder.Services.AddSingleton<IConversationIdFactory, ConversationIdFactory>();

            // Add bot callback handler.
            // This is the object that handles callback endpoints for bot responses.
            builder.Services.AddTransient<IChannelResponseHandler>((sp) => sp.GetService<Bot1>());

            return builder;
        }
    }
}
