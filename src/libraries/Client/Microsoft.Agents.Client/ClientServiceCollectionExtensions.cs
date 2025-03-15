using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Client
{
    public static class ClientServiceCollectionExtensions
    {
        /// <summary>
        /// Adds bot-to-bot functionality.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="httpBotClientName"></param>
        /// <param name="storage">Used for ConversationIdFactory.  If null, the registered IStorage will be used.</param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddChannelHost(this IHostApplicationBuilder builder, string httpBotClientName = "HttpBotClient", IStorage storage = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(httpBotClientName);

            // Add conversation id factory.  
            // This is a memory only implementation, and for production would require persistence.
            if (storage != null)
            {
                builder.Services.AddSingleton<IConversationIdFactory>(sp => new ConversationIdFactory(storage));
            }
            else
            {
                builder.Services.AddSingleton<IConversationIdFactory, ConversationIdFactory>();
            }

            // Add bot callback handler.  This is AgentApplication specific.
            // This is the object that handles callback endpoints for bot responses.
            builder.Services.AddTransient<AdapterBotResponseHandler>();
            builder.Services.AddTransient<IChannelApiHandler>((sp) => sp.GetService<AdapterBotResponseHandler>());

            // Add the bots configuration class.  This loads client info and known bots.
            builder.Services.AddSingleton<IChannelHost, ConfigurationChannelHost>(
                (sp) =>
                new ConfigurationChannelHost(
                    sp.GetRequiredService<IServiceProvider>(),
                    sp.GetRequiredService<IConversationIdFactory>(),
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

            return builder;
        }
    }
}
