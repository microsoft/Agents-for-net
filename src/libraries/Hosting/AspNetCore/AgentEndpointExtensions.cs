// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public static class AgentEndpointExtensions
    {
        /// <summary>
        /// The delegate signature for custom AgentApplication request processing methods when specified in AgentInterfaceAttribute.ProcessDelegate.
        /// </summary>
        /// <param name="request">The HTTP request object, typically in a POST handler by a Controller.</param>
        /// <param name="response">The HTTP response object.</param>
        /// <param name="adapter">The IAgentHttpAdapter for this request.</param>
        /// <param name="agent">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public delegate Task ProcessRequestDelegate(HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken);
        public delegate Task ProcessRequestDelegate<TAdapter, TAgent>(HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter;

        /// <summary>
        /// This adds HTTP endpoints for all AgentApplications defined in the calling assembly.  Each AgentApplication must have been added using <see cref="AddAgent{TAgent}(IHostApplicationBuilder)"/>.
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="requireAuth"></param>
        /// <param name="defaultPath"></param>
        /// <exception cref="InvalidOperationException"/>
        public static IEndpointConventionBuilder MapAgentApplicationEndpoints(
            this IEndpointRouteBuilder endpoints,
            bool requireAuth = true,
            [StringSyntax("Route")] string defaultPath = "/api/messages")
        {
            if (string.IsNullOrEmpty(defaultPath))
            {
                defaultPath = "/api/messages";
            }

            var agentGroup = endpoints.MapGroup("");
            if (requireAuth)
            {
                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            var allAgents = Assembly.GetCallingAssembly().GetTypes().Where(t => t.IsOrDerives(typeof(AgentApplication))).ToList();
            if (allAgents.Count == 0)
            {
                // This is to handle declaring an AgentApplication in an AddTransient lambda.
                var inlineAgent = endpoints.ServiceProvider.GetService<IAgent>()
                    ?? throw new InvalidOperationException("No AgentApplications were found in the calling assembly. Ensure that at least one AgentApplication is defined.");
                allAgents.Add(inlineAgent.GetType());
            }

            foreach (var agent in allAgents)
            {
                var interfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true)?.ToList();
                if (interfaces?.Count == 0)
                {
                    if (allAgents.Count == 1)
                    {
                        // If there is only one AgentApplication, we can default
                        interfaces = new List<AgentInterfaceAttribute>()
                        {
                            new(AgentTransportProtocol.ActivityProtocol, defaultPath)
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"No AgentInterfaceAttribute was found on Agent '{agent.FullName}'. When multiple AgentApplications are defined, each must have at least one AgentInterfaceAttribute.");
                    }
                }

                foreach (var agentInterface in interfaces)
                {
                    if (agentInterface.Protocol != AgentTransportProtocol.ActivityProtocol)
                    {
                        // Currently only ActivityProtocol is supported here.
                        continue;
                    }

                    agentGroup.MapMethods(agentInterface.Path, ["POST"],
                        async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IServiceProvider services, CancellationToken cancellationToken) =>
                        {
                            IAgent agentInstance = (IAgent)services.GetService(agent);
                            // This is to handle declaring an AgentApplication in an AddTransient lambda.
                            agentInstance ??= (IAgent)services.GetRequiredService(typeof(IAgent));

                            if (!string.IsNullOrEmpty(agentInterface.ProcessDelegate))
                            {
                                var processMethod = agentInstance.GetType().GetMethod(agentInterface.ProcessDelegate, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? throw new InvalidOperationException($"The specified ProcessDelegate '{agentInterface.ProcessDelegate}' was not found on AgentApplication '{agentInstance.GetType().FullName}'.");

#if !NETSTANDARD
                                var processDelegate = processMethod.CreateDelegate<ProcessRequestDelegate>(agentInstance);
#else
                                var processDelegate = (ProcessRequestDelegate)processMethod.CreateDelegate(typeof(ProcessRequestDelegate), agentInstance);
#endif

                                await processDelegate(request, response, adapter, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                await adapter.ProcessAsync(request, response, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                        });
                }
            }

            return agentGroup;
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, IAgent> process = null)
        {
            return AgentEndpointExtensions.MapAgentEndpoints<IAgentHttpAdapter, IAgent>(app, requireAuth, path, (ProcessRequestDelegate<IAgentHttpAdapter, IAgent>)process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints<TAgent>(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, TAgent> process = null)
            where TAgent : IAgent
        {
            return AgentEndpointExtensions.MapAgentEndpoints<IAgentHttpAdapter, TAgent>(app, requireAuth, path, (ProcessRequestDelegate<IAgentHttpAdapter, TAgent>)process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints<TAdapter, TAgent>(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<TAdapter, TAgent> process = null)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter
        {
            var agentGroup = app.MapGroup("");
            if (requireAuth)
            {
                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            // This receives incoming messages from Azure Bot Service or other SDK Agents
            agentGroup.MapPost(path, async (HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken) =>
            {
                if (process != null)
                {
                    await process(request, response, adapter, agent, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await adapter.ProcessAsync(request, response, agent, cancellationToken).ConfigureAwait(false);
                }
            });

            return agentGroup;
        }

        /// <summary>
        /// Maps the root endpoint ('/') of the web application to return the assembly name and version information for
        /// the Microsoft Agents SDK.
        /// </summary>
        /// <remarks>The root endpoint provides a simple informational response containing the name and
        /// version of the calling assembly. This can be useful for diagnostics or verifying deployment
        /// details.</remarks>
        /// <param name="app">The web application instance to which the root endpoint will be mapped.</param>
        public static void MapAgentRootEndpoint(this WebApplication app)
        {
            var assemblyName = System.Reflection.Assembly.GetCallingAssembly().GetName().Name;
            var assemblyVersion = System.Reflection.Assembly.GetCallingAssembly().GetName().Version?.ToString() ?? "unknown";
            app.MapGet("/", () => $"Microsoft Agents SDK: {assemblyName}, version {assemblyVersion}");
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