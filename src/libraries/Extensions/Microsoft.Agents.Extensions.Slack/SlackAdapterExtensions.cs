// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack
{
    /// <summary>
    /// Registration and endpoint-mapping helpers that wire the <see cref="SlackAdapter"/> for direct
    /// Slack ingestion (no Azure Bot Service).
    /// </summary>
    public static class SlackAdapterExtensions
    {
        /// <summary>
        /// The <see cref="AgentInterfaceAttribute.Protocol"/> value that identifies a Slack ingestion
        /// endpoint. Annotate an <c>AgentApplication</c> with
        /// <c>[AgentInterface(SlackAdapterExtensions.SlackProtocol, "/api/slack")]</c> and call
        /// <see cref="MapSlackEndpoints"/> to expose it.
        /// </summary>
        public const string SlackProtocol = "slack";

        /// <summary>The default configuration section that <see cref="SlackAdapterOptions"/> is bound from.</summary>
        public const string DefaultConfigSection = "Slack";

        /// <summary>The route used when an <see cref="AgentInterfaceAttribute"/> does not specify a path.</summary>
        public const string DefaultRoute = "/api/slack";

        /// <summary>
        /// Registers the <see cref="SlackAdapter"/> and its <see cref="SlackAdapterOptions"/> (bound from the
        /// specified configuration section) so Slack traffic can be received directly.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="configSection">The configuration section to bind options from. Defaults to <c>"Slack"</c>.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSlack(this IServiceCollection services, IConfiguration configuration, string configSection = DefaultConfigSection)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var options = configuration.GetSection(configSection).Get<SlackAdapterOptions>() ?? new SlackAdapterOptions();
            return services.AddSlack(options);
        }

        /// <summary>
        /// Registers the <see cref="SlackAdapter"/> using the supplied configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">A delegate that populates the <see cref="SlackAdapterOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSlack(this IServiceCollection services, Action<SlackAdapterOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new SlackAdapterOptions();
            configure(options);
            return services.AddSlack(options);
        }

        /// <summary>
        /// Registers the <see cref="SlackAdapter"/> using the supplied options instance.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The Slack options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSlack(this IServiceCollection services, SlackAdapterOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            services.AddAsyncAdapterSupport();
            services.AddHttpClient();
            services.AddSingleton(options);
            services.AddSingleton(provider => new SlackRequestValidator(
                provider.GetRequiredService<SlackAdapterOptions>()));
            services.AddSingleton(_ => new SlackRequestParser());
            services.AddSingleton(_ => new SlackEventDeduplicator());
            services.AddSingleton(provider => new SlackActivityConverter(
                provider.GetRequiredService<SlackAdapterOptions>()));
            services.AddSingleton(provider => new SlackApi(
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<ILogger<SlackApi>>()));
            services.AddSingleton<ISlackFileUploader>(provider => new SlackFileUploader(
                provider.GetRequiredService<SlackApi>()));
            services.AddSingleton(provider => new SlackAttachmentConverter(
                provider.GetRequiredService<ISlackFileUploader>(),
                provider.GetRequiredService<ILogger<SlackAttachmentConverter>>()));
            services.AddSingleton(provider => new SlackMessageConverter(
                provider.GetRequiredService<SlackAttachmentConverter>()));
            services.AddSingleton(provider => new SlackAdapter(
                provider.GetRequiredService<SlackAdapterOptions>(),
                provider.GetRequiredService<SlackApi>(),
                provider.GetRequiredService<ILogger<SlackAdapter>>(),
                provider.GetRequiredService<IActivityTaskQueue>(),
                provider.GetRequiredService<SlackRequestValidator>(),
                provider.GetRequiredService<SlackRequestParser>(),
                provider.GetRequiredService<SlackEventDeduplicator>(),
                provider.GetRequiredService<SlackActivityConverter>(),
                provider.GetRequiredService<SlackMessageConverter>()));
            services.AddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<SlackAdapter>());

            return services;
        }

        /// <summary>
        /// Maps HTTP POST endpoints for every <c>AgentApplication</c> in the calling assembly that declares an
        /// <see cref="AgentInterfaceAttribute"/> whose <see cref="AgentInterfaceAttribute.Protocol"/> is
        /// <see cref="SlackProtocol"/>. Each such interface's <see cref="AgentInterfaceAttribute.Path"/> is
        /// forwarded to the <see cref="SlackAdapter"/> for direct Slack ingestion.
        /// </summary>
        /// <remarks>
        /// This mirrors <c>MapAgentApplicationEndpoints</c> but for the Slack transport: Slack posts raw
        /// Events API / Interactivity payloads (not Activity Protocol JSON) to a dedicated URL, so it needs
        /// its own endpoint bound to the <see cref="SlackAdapter"/> rather than the shared Activity endpoint.
        /// Configure each mapped URL as the Request URL for both Event Subscriptions and Interactivity in
        /// your Slack app.
        /// </remarks>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="requireAuth">
        /// When <see langword="true"/>, the mapped endpoints require authorization. Defaults to
        /// <see langword="false"/> because the <see cref="SlackAdapter"/> authenticates inbound requests by
        /// verifying the Slack request signature against the configured signing secret.
        /// </param>
        /// <returns>The endpoint convention builder for further configuration.</returns>
        public static IEndpointConventionBuilder MapSlackEndpoints(this IEndpointRouteBuilder endpoints, bool requireAuth = false)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var slackGroup = endpoints.MapGroup("");
            if (requireAuth)
            {
                slackGroup.RequireAuthorization();
            }
            else
            {
                slackGroup.AllowAnonymous();
            }

            var allAgents = Assembly.GetCallingAssembly().GetTypes()
                .Where(t => t.IsOrDerives(typeof(AgentApplication)))
                .ToList();

            if (allAgents.Count == 0)
            {
                // Handle declaring an AgentApplication in an AddTransient lambda.
                var inlineAgent = endpoints.ServiceProvider.GetService<IAgent>()
                    ?? throw new InvalidOperationException("No AgentApplications were found in the calling assembly. Ensure that at least one AgentApplication is defined.");
                allAgents.Add(inlineAgent.GetType());
            }

            foreach (var agent in allAgents)
            {
                var slackInterfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true)?
                    .Where(i => string.Equals(i.Protocol, SlackProtocol, StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? new List<AgentInterfaceAttribute>();

                foreach (var agentInterface in slackInterfaces)
                {
                    var path = string.IsNullOrEmpty(agentInterface.Path) ? DefaultRoute : agentInterface.Path;

                    slackGroup.MapMethods(path, ["POST"],
                        async (HttpRequest request, HttpResponse response, SlackAdapter adapter, IServiceProvider services, CancellationToken cancellationToken) =>
                        {
                            IAgent agentInstance = (IAgent)services.GetService(agent);
                            // Handle declaring an AgentApplication in an AddTransient lambda.
                            agentInstance ??= services.GetRequiredService<IAgent>();

                            if (!string.IsNullOrEmpty(agentInterface.ProcessDelegate))
                            {
                                var processMethod = agentInstance.GetType().GetMethod(agentInterface.ProcessDelegate, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? throw new InvalidOperationException($"The specified ProcessDelegate '{agentInterface.ProcessDelegate}' was not found on AgentApplication '{agentInstance.GetType().FullName}'.");

                                var processDelegate = processMethod.CreateDelegate<AgentEndpointExtensions.ProcessRequestDelegate>(agentInstance);
                                await processDelegate(request, response, adapter, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                await adapter.ProcessAsync(request, response, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                        });
                }
            }

            return slackGroup;
        }

        private static bool IsOrDerives(this Type type, Type baseType)
        {
            if (type.Equals(baseType))
            {
                return true;
            }

            var current = type.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType))
                {
                    return true;
                }
                current = current.BaseType;
            }

            return false;
        }
    }
}
