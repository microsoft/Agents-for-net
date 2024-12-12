// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Protocols.Connector;

namespace Microsoft.Agents.Hosting.Setup
{
    public static class SampleServiceCollectionExtensions
    {
        public static IHostApplicationBuilder AddBot<T, TImpl>(this IHostApplicationBuilder builder)
            where T : IBot
            where TImpl : class, T
        {
            builder.Services.AddBotAspNetAuthentication(builder.Configuration);

            // Add Connections object to access configured token connections.
            builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

            // Add factory for ConnectorClient and UserTokenClient creation
            builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

            // Add the BotAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol.
            builder.Services.AddCloudAdapter();

            // Add the Bot,  this is the primary worker for the bot. 
            builder.Services.AddTransient<IBot, TImpl>();

            return builder;
        }
    }
}
