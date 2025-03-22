// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Client.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Loads Agent information from configuration.
    /// </summary>
    public class ConfigurationAgentHost : IAgentHost
    {
        public const string AgentConversationsProperty = "agentHost.agentConversations";

        private readonly IServiceProvider _serviceProvider;
        private readonly IConversationIdFactory _conversationIdFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;
        internal IDictionary<string, HttpAgentClientSettings> _agents;

        public ConfigurationAgentHost(
            IServiceProvider systemServiceProvider,
            IStorage storage,
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            IDictionary<string, HttpAgentClientSettings> agents,
            string hostEndpoint,
            string hostClientId)
        {
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));

            _conversationIdFactory = new ConversationIdFactory(storage);

            if (!string.IsNullOrWhiteSpace(hostEndpoint))
            {
                DefaultResponseEndpoint = new Uri(hostEndpoint);
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentHostMissingProperty, null, nameof(DefaultResponseEndpoint));
            }

            if (!string.IsNullOrWhiteSpace(hostClientId))
            {
                HostClientId = hostClientId;
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentHostMissingProperty, null, nameof(HostClientId));
            }

            LoadAgents(agents);
        }

        /// <summary>
        /// Creates from IConfiguration.
        /// </summary>
        /// <code>
        /// "Agent": {
        ///   "ClientId": "{{ClientId}}",                                  // This is the Client ID used for the remote agent to call you back with.,
        ///   "Description": null,                                         // Optional
        ///   "Publisher": null,                                           // Optional
        ///   "Copyright": null,                                           // Optional
        ///   "Host": {
        ///     "DefaultResponseEndpoint": "http://localhost:3978/api/agentresponse/", // Default host serviceUrl.  Agent can override this via Agents:{{name}}:ConnectionSettings:ServiceUrl
        ///     "Agents": {
        ///       "Echo": {
        ///         "DisplayName": {{optional-displayName}},               // Defaults to node name ("Echo")
        ///         "ConnectionSettings": {
        ///           "ClientId": "{{Agent2ClientId}}",                    // This is the Client ID of the other agent.
        ///           "Endpoint": "http://localhost:39783/api/messages",   // The endpoint of the other agent
        ///           "TokenProvider" : "{{Connections:{{name}}"
        ///         }
        ///       }
        ///     }
        ///   }
        /// }
        /// </code>
        /// <param name="configuration"></param>
        /// <param name="systemServiceProvider"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="configSection"></param>
        public ConfigurationAgentHost(
            IConfiguration configuration,
            IServiceProvider systemServiceProvider,
            IStorage storage, 
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            string configSection = "Agent") : this(
                systemServiceProvider, 
                storage, 
                connections, 
                httpClientFactory,
                configuration?.GetSection($"{configSection}:Host:Agents").Get<IDictionary<string, HttpAgentClientSettings>>(), 
                configuration?.GetValue<string>($"{configSection}:Host:DefaultResponseEndpoint"), 
                configuration?.GetValue<string>($"{configSection}:ClientId"))
        {
        }

        private void LoadAgents(IDictionary<string, HttpAgentClientSettings> agents)
        {
            _agents = agents ?? new Dictionary<string, HttpAgentClientSettings>();
            if (agents != null)
            {
                foreach (var kv in _agents)
                {
                    // If the ServiceUrl isn't supplied at the Agent:ConnectionSettings use
                    // the DefaultHostEndpoint.
                    if (string.IsNullOrEmpty(kv.Value.ConnectionSettings.ServiceUrl))
                    {
                        kv.Value.ConnectionSettings.ServiceUrl = DefaultResponseEndpoint.ToString();
                    }

                    kv.Value.Name = kv.Key;

                    if (string.IsNullOrEmpty(kv.Value.DisplayName))
                    {
                        kv.Value.DisplayName = kv.Value.Name;
                    }

                    kv.Value.ValidateClientSettings();
                }
            }
        }

        /// <inheritdoc />
        public Uri DefaultResponseEndpoint { get; set; }

        /// <inheritdoc />
        public string HostClientId { get; set; }

        /// <inheritdoc/>
        public IAgentClient GetClient(string agentName)
        {
            if (string.IsNullOrWhiteSpace(agentName))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentNotFound, null, "<null>");
            }

            if (!_agents.TryGetValue(agentName, out HttpAgentClientSettings clientSettings))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentNotFound, null, agentName);
            }

            return CreateClient(agentName, clientSettings);
        }

        /// <inheritdoc />
        public IList<IAgentInfo> GetAgents()
        {
            var result = new List<IAgentInfo>();

            foreach (var agent in _agents.Values)
            {
                result.Add(agent);
            }

            return result;
        }

        /// <inheritdoc/>
        public string GetConversation(ITurnContext turnContext, ConversationState conversationState, string agentName)
        {
            var conversations = conversationState.GetValue<IDictionary<string, AgentConversation>>(AgentConversationsProperty, () => new Dictionary<string, AgentConversation>());
            if (conversations.TryGetValue(agentName, out var conversation)) 
            { 
                return conversation.AgentConversationId;
            }
            return null;
        }

        /// <inheritdoc/>
        public IList<AgentConversation> GetConversations(ITurnContext turnContext, ConversationState conversationState)
        {
            var result = new List<AgentConversation>();
            var conversations = conversationState.GetValue<IDictionary<string, AgentConversation>>(AgentConversationsProperty, () => new Dictionary<string, AgentConversation>());
            foreach (var conversation in conversations)
            {
                result.Add(conversation.Value);
            }
            return result;
        }

        /// <inheritdoc/>
        public async Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, ConversationState conversationState, string agentName, CancellationToken cancellationToken = default)
        {
            var conversations = conversationState.GetValue<IDictionary<string, AgentConversation>>(AgentConversationsProperty, () => new Dictionary<string, AgentConversation>());
            if (conversations.TryGetValue(agentName, out var conversation)) 
            { 
                // Already a conversation for the Agent.
                return conversation.AgentConversationId; 
            }

            var options = new ConversationIdFactoryOptions
            {
                FromOAuthScope = BotClaims.GetTokenScopes(turnContext.Identity)?.First(),
                FromClientId = HostClientId,
                Activity = turnContext.Activity,
                Agent = _agents[agentName]
            };

            var agentConversationId = await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken).ConfigureAwait(false);
            conversations.Add(agentName, new AgentConversation() { AgentConversationId = agentConversationId, AgentName = agentName });
            return agentConversationId;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationAsync(string agentConversationId, ConversationState conversationState, CancellationToken cancellationToken = default)
        {
            await _conversationIdFactory.DeleteConversationReferenceAsync(agentConversationId, cancellationToken).ConfigureAwait(false);

            var conversations = conversationState.GetValue<IDictionary<string, AgentConversation>>(AgentConversationsProperty, () => new Dictionary<string, AgentConversation>());
            var agentName = conversations.Where(kv => kv.Value.AgentConversationId.Equals(agentConversationId)).Select((kv) => kv.Key).FirstOrDefault();
            if (agentName != null)
            {
                conversations.Remove(agentName);
            }
        }

        /// <inheritdoc/>
        public async Task SendToAgent(string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var client = GetClient(agentName);
            
            if (string.IsNullOrEmpty(activity.DeliveryMode)
                || !string.Equals(DeliveryModes.Normal, activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                activity.DeliveryMode = DeliveryModes.Normal;
            }

            await client.SendActivityAsync<object>(agentConversationId, activity, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<object> SendToAgentStreamedAsync(string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var client = GetClient(agentName);
            return client.SendActivityStreamedAsync(agentConversationId, activity, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task EndAllConversations(ITurnContext turnContext, ConversationState conversationState, CancellationToken cancellationToken = default)
        {
            // End all active Agent conversations.
            var activeConversations = GetConversations(turnContext, conversationState);
            if (activeConversations.Count > 0)
            {
                foreach (var conversation in activeConversations)
                {
                    // Delete the conversation because we're done with it.
                    await DeleteConversationAsync(conversation.AgentConversationId, conversationState, cancellationToken).ConfigureAwait(false);

                    // Send EndOfConversation to the Agent.
                    await SendToAgent(conversation.AgentName, conversation.AgentConversationId, Activity.CreateEndOfConversationActivity(), cancellationToken).ConfigureAwait(false);
                }
            }

            await conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<AgentConversationReference> GetConversationReferenceAsync(string agentConversationId, CancellationToken cancellationToken = default)
        {
            return _conversationIdFactory.GetAgentConversationReferenceAsync(agentConversationId, cancellationToken);
        }

        private IAgentClient CreateClient(string agentName, HttpAgentClientSettings clientSettings)
        {
            var tokenProviderName = clientSettings.ConnectionSettings.TokenProvider;
            if (!_connections.TryGetConnection(tokenProviderName, out var tokenProvider))
            {
                throw new ArgumentException($"TokenProvider '{tokenProviderName}' not found for Agent '{agentName}'");
            }

            return new HttpAgentClient(clientSettings, _httpClientFactory, tokenProvider, (ILogger<HttpAgentClient>) _serviceProvider.GetService(typeof(ILogger<HttpAgentClient>)));
        }
    }
}
