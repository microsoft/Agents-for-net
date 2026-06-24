// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Marker service registered by <see cref="AgentHostExtensions.AddAgentAuthorization"/> to indicate
    /// that token validation was configured. Used by <see cref="AgentHostExtensions.MapDefaultAgentEndpoints"/>
    /// to determine whether endpoints should require authorization.
    /// </summary>
    internal sealed class AgentAuthConfigured { }

    /// <summary>
    /// Extension methods on <see cref="IHostApplicationBuilder"/> and <see cref="WebApplication"/>
    /// for configuring agent services and the request pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These methods provide a streamlined way to configure an agent application. The minimal setup is:
    /// <code>
    /// builder.AddAgentDefaults()
    ///     .AddAgent&lt;MyAgent&gt;()
    ///     .AddAgentAuthorization(b =&gt; b.AddAgentAspNetAuthentication());
    ///
    /// var app = builder.Build();
    ///
    /// app.UseAgents()
    ///     .MapDefaultAgentEndpoints();
    /// </code>
    /// </para>
    /// <para>
    /// For advanced scenarios (custom <c>HttpClient</c> configuration, non-standard middleware ordering,
    /// custom endpoint routing, etc.), use <see cref="ServiceCollectionExtensions"/> for DI registration
    /// and <see cref="AgentEndpointExtensions"/> for endpoint mapping directly. The equivalent manual
    /// setup for the above is:
    /// <code>
    /// builder.Services.AddHttpClient();
    /// builder.Services.AddControllers();
    /// builder.AddAgent&lt;MyAgent&gt;();
    /// // Configure authentication (e.g., AddAgentAspNetAuthentication, MISE, or custom)
    ///
    /// var app = builder.Build();
    ///
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapAgentRootEndpoint();
    /// app.MapAgentApplicationEndpoints(requireAuth: true);
    /// </code>
    /// </para>
    /// </remarks>
    public static class AgentHostExtensions
    {
        #region Fluent Builder API

        /// <summary>
        /// Registers default services required by the Agents SDK.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// <para>This registers:</para>
        /// <list type="bullet">
        /// <item><description><c>IHttpClientFactory</c> via <c>AddHttpClient()</c> — used internally by the SDK
        /// for outbound HTTP calls (e.g., Bot Connector, token endpoints).</description></item>
        /// <item><description>MVC Controllers via <c>AddControllers()</c> — required for ASP.NET authorization
        /// services.</description></item>
        /// </list>
        /// <para>
        /// <c>AddHttpClient()</c> is idempotent and registers a default <c>IHttpClientFactory</c>.
        /// If you need named or typed clients (e.g., for custom <c>DelegatingHandler</c> pipelines or
        /// specific timeouts), register them separately via <c>builder.Services.AddHttpClient("name", ...)</c>
        /// — this does not conflict with the default registration.
        /// </para>
        /// <para>
        /// If you need full control over HTTP client configuration for SDK internals, skip this method
        /// and use <see cref="ServiceCollectionExtensions"/> directly to register services manually.
        /// </para>
        /// </remarks>
        public static IHostApplicationBuilder AddAgentDefaults(this IHostApplicationBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddControllers();
            return builder;
        }

        /// <summary>
        /// Configures token validation for inbound requests. The provided action should register
        /// authentication services (e.g., call <c>AddAgentAspNetAuthentication</c>).
        /// When enabled, endpoints mapped by <see cref="MapDefaultAgentEndpoints"/> will automatically
        /// require authorization.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="configure">Action that configures authentication on the builder. The sample-provided
        /// <c>AddAgentAspNetAuthentication</c> is the default implementation, but developers may use MISE
        /// or any other ASP.NET Core authentication mechanism.</param>
        /// <param name="forceEnable">
        /// Override for whether authorization is enabled. When <c>null</c> (the default), authorization is
        /// enabled in all environments except Development. Pass an explicit value to override — for example:
        /// <c>forceEnable: !builder.Environment.IsDevelopment() &amp;&amp; !builder.Environment.IsEnvironment("Staging")</c>.
        /// </param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// <para>
        /// When <paramref name="forceEnable"/> resolves to <c>true</c>, the <paramref name="configure"/> action
        /// is invoked and a marker service is registered in DI. <see cref="MapDefaultAgentEndpoints"/> checks
        /// for this marker to decide whether to call <c>RequireAuthorization()</c> on agent endpoints.
        /// </para>
        /// <para>
        /// If this method is not called or <paramref name="forceEnable"/> resolves to <c>false</c>,
        /// no authentication is configured and endpoints will allow anonymous access.
        /// </para>
        /// <para>
        /// To configure authentication manually without this method, register your authentication
        /// services directly on <c>builder.Services</c> and use
        /// <see cref="AgentEndpointExtensions.MapAgentApplicationEndpoints"/> with <c>requireAuth: true</c>.
        /// </para>
        /// </remarks>
        public static IHostApplicationBuilder AddAgentAuthorization(this IHostApplicationBuilder builder, Action<IHostApplicationBuilder> configure, bool? forceEnable = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            bool shouldEnable = forceEnable ?? !builder.Environment.IsDevelopment();

            if (shouldEnable)
            {
                configure(builder);
                builder.Services.AddSingleton<AgentAuthConfigured>();
            }

            return builder;
        }

        /// <summary>
        /// Registers an <see cref="IMiddleware"/> instance to be used by the <see cref="CloudAdapter"/>
        /// pipeline. Can be called multiple times to add additional middleware in order.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="middleware">The middleware instance to add.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// Middleware registered here runs in the adapter pipeline (before <c>AgentApplication</c> routing).
        /// Each call appends to an internal list that is resolved as <c>IMiddleware[]</c> by the
        /// <see cref="CloudAdapter"/> constructor.
        /// </remarks>
        public static IHostApplicationBuilder AddAgentMiddleware(this IHostApplicationBuilder builder, IMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            var list = GetOrCreateMiddlewareList(builder.Services);
            list.Add(middleware);

            return builder;
        }

        /// <summary>
        /// Registers the <see cref="HeaderPropagationMiddleware"/> so that incoming request headers are
        /// propagated to outgoing requests. This builder-phase overload allows header propagation to be
        /// configured fluently alongside the other agent registration calls:
        /// <code>
        /// builder.AddAgentDefaults()
        ///     .AddAgent&lt;MyAgent&gt;()
        ///     .UseHeaderPropagation()
        ///     .AddAgentAuthorization(b =&gt; b.AddAgentAspNetAuthentication());
        /// </code>
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// This registers an <see cref="IStartupFilter"/> that inserts the middleware at the start of the
        /// request pipeline, ensuring request headers are captured before any agent endpoints run. This is
        /// equivalent to calling <see cref="UseHeaderPropagation(IApplicationBuilder)"/> on the built
        /// <see cref="WebApplication"/>; use one or the other, not both.
        /// </remarks>
        public static IHostApplicationBuilder UseHeaderPropagation(this IHostApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddTransient<IStartupFilter, HeaderPropagationStartupFilter>();
            return builder;
        }

        /// <summary>
        /// Registers an <see cref="IInputFileDownloader"/> instance for use by <see cref="AgentApplicationOptions"/>.
        /// Can be called multiple times to add additional downloaders.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="downloader">The file downloader instance to add.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// Downloaders are resolved as <c>IList&lt;IInputFileDownloader&gt;</c> from DI and are
        /// used by <c>AgentApplication</c> to process inbound file attachments.
        /// </remarks>
        public static IHostApplicationBuilder AddAgentFileDownloader(this IHostApplicationBuilder builder, IInputFileDownloader downloader)
        {
            ArgumentNullException.ThrowIfNull(downloader);

            var list = GetOrCreateFileDownloaderList(builder.Services);
            list.Add(downloader);

            return builder;
        }

        /// <summary>
        /// Registers the <see cref="AttachmentDownloader"/> for non-Teams channels.
        /// This downloader handles standard HTTP attachment URLs.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// This is a convenience method equivalent to:
        /// <code>
        /// builder.AddAgentFileDownloader(new AttachmentDownloader(httpClientFactory));
        /// </code>
        /// The <c>IHttpClientFactory</c> dependency is resolved at runtime from DI.
        /// This method should only be called once.
        /// </remarks>
        public static IHostApplicationBuilder AddAgentAttachmentDownloader(this IHostApplicationBuilder builder)
        {
            GetOrCreateFileDownloaderList(builder.Services);
            builder.Services.AddSingleton<DeferredAttachmentDownloader>();

            return builder;
        }

        /// <summary>
        /// Registers the <see cref="M365AttachmentDownloader"/> for Teams/M365 channels.
        /// This downloader handles M365 attachment URLs using the configured token provider.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="options">Optional configuration for the M365 downloader.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// This is a convenience method equivalent to:
        /// <code>
        /// builder.AddAgentFileDownloader(new M365AttachmentDownloader(connections, httpClientFactory));
        /// </code>
        /// The <c>IConnections</c> and <c>IHttpClientFactory</c> dependencies are resolved at runtime from DI.
        /// This method should only be called once.
        /// </remarks>
        public static IHostApplicationBuilder AddAgentM365AttachmentDownloader(this IHostApplicationBuilder builder, M365AttachmentDownloaderOptions options = null)
        {
            GetOrCreateFileDownloaderList(builder.Services);

            if (options != null)
            {
                builder.Services.AddSingleton(options);
            }

            builder.Services.AddSingleton<DeferredM365AttachmentDownloader>();

            return builder;
        }

        #endregion

        #region IHostApplicationBuilder Extensions

        /// <summary>
        /// Adds an Agent which subclasses <c>AgentApplication</c>
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgent&lt;MyAgent>();
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also call <see cref="AddAgentCore(IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient. <see cref="AgentApplicationOptions"/> is automatically registered
        /// if not already present.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent<TAgent>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
        {
            return builder.AddAgent<TAgent, CloudAdapter>();
        }

        /// <summary>
        /// Same as <see cref="AddAgent{TAgent}(IHostApplicationBuilder)"/> but allows for use of
        /// any <c>CloudAdapter</c> subclass.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgent&lt;MyAgent, MyCustomAdapter&gt;();
        /// </code>
        /// </summary>
        /// <remarks>
        /// <see cref="AgentApplicationOptions"/> is automatically registered if not already present.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent<TAgent, TAdapter>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
            where TAdapter : CloudAdapter
        {
            builder.Services.AddAgent<TAgent, TAdapter>();
            return builder;
        }

        /// <summary>
        /// Adds an Agent via lambda construction.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
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
        /// This will also call <see cref="AddAgentCore(IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient. <see cref="AgentApplicationOptions"/> is automatically registered
        /// if not already present.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
        {
            return builder.AddAgent<CloudAdapter>(implementationFactory);
        }

        /// <summary>
        /// This is the same as <see cref="AddAgent(IHostApplicationBuilder, Func{IServiceProvider, IAgent})"/>, except allows the
        /// use of any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <remarks>
        /// <see cref="AgentApplicationOptions"/> is automatically registered if not already present.
        /// </remarks>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgent<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
            where TAdapter : CloudAdapter
        {
            builder.Services.AddAgent<TAdapter>(implementationFactory);
            return builder;
        }

        /// <summary>
        /// Add the default CloudAdapter.
        /// </summary>
        /// <param name="builder">The host application builder to which the cloud adapter services will be added. Cannot be null.</param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddCloudAdapter(this IHostApplicationBuilder builder)
        {
            builder.Services.AddCloudAdapter();
            return builder;
        }

        /// <summary>
        /// Add a derived CloudAdapter.
        /// </summary>
        /// <param name="builder">The host application builder to which the cloud adapter services will be added. Cannot be null.</param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddCloudAdapter<T>(this IHostApplicationBuilder builder) where T : CloudAdapter
        {
            builder.Services.AddCloudAdapter<T>();
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
        /// <param name="autoSignIn"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentApplicationOptions(this IHostApplicationBuilder builder, AutoSignInSelector autoSignIn = null)
        {
            builder.Services.AddAgentApplicationOptions(autoSignIn);
            return builder;
        }

        /// <summary>
        /// Adds the core agent services.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentCore(this IHostApplicationBuilder builder)
        {
            return builder.AddAgentCore<CloudAdapter>();
        }

        /// <summary>
        /// Adds the core agent services using a derived CloudAdapter.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>The same instance of <see cref="IHostApplicationBuilder"/> to allow for method chaining.</returns>
        public static IHostApplicationBuilder AddAgentCore<TAdapter>(this IHostApplicationBuilder builder) where TAdapter : CloudAdapter
        {
            builder.Services.AddAgentCore<TAdapter>();
            return builder;
        }

        #endregion

        #region WebApplication Extensions

        /// <summary>
        /// Adds authentication and authorization middleware to the request pipeline.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="useRouting">
        /// When <see langword="true"/>, calls <c>app.UseRouting()</c> before the authentication and
        /// authorization middleware. Routing is order dependent and must be added before
        /// authentication/authorization. Set this to <see langword="false"/> (the default) if routing is
        /// already configured elsewhere in the pipeline, otherwise it will be added twice.
        /// </param>
        /// <returns>The same <see cref="WebApplication"/> for chaining.</returns>
        /// <remarks>
        /// <para>This calls <c>app.UseAuthentication()</c> and <c>app.UseAuthorization()</c> in the
        /// correct order. Call this before <see cref="MapDefaultAgentEndpoints"/>.</para>
        /// <para>
        /// If <paramref name="useRouting"/> is <see langword="true"/>, <c>app.UseRouting()</c> is called
        /// first, since routing must be registered before authentication and authorization.
        /// </para>
        /// <para>
        /// If you need to insert additional middleware between authentication and authorization
        /// (e.g., custom claims transformation), call <c>UseAuthentication()</c> and <c>UseAuthorization()</c>
        /// manually instead of using this method.
        /// </para>
        /// </remarks>
        public static WebApplication UseAgents(this WebApplication app, bool useRouting = false)
        {
            if (useRouting)
            {
                app.UseRouting();
            }

            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }

        /// <summary>
        /// Maps the default agent endpoints: a root health endpoint (<c>GET /</c>) and agent message
        /// endpoints for all registered <c>AgentApplication</c> types.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="path">The route path for agent message endpoints. Defaults to <c>"/api/messages"</c>.</param>
        /// <returns>The same <see cref="WebApplication"/> for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Authorization is automatically required if <see cref="AddAgentAuthorization"/> was called
        /// during service configuration. Otherwise, endpoints allow anonymous access.
        /// </para>
        /// <para>This is equivalent to calling:</para>
        /// <code>
        /// app.MapAgentRootEndpoint();
        /// app.MapAgentApplicationEndpoints(requireAuth: true/false, defaultPath: path);
        /// </code>
        /// <para>
        /// For custom endpoint routing (e.g., different paths per agent, additional middleware per route,
        /// or manual <c>requireAuth</c> control), use <see cref="AgentEndpointExtensions.MapAgentRootEndpoint"/>
        /// and <see cref="AgentEndpointExtensions.MapAgentApplicationEndpoints"/> directly.
        /// </para>
        /// </remarks>
        public static WebApplication MapDefaultAgentEndpoints(this WebApplication app, string path = "/api/messages")
        {
            bool requireAuth = app.Services.GetService<AgentAuthConfigured>() != null;

            app.MapAgentRootEndpoint();
            app.MapAgentApplicationEndpoints(requireAuth: requireAuth, defaultPath: path);

            return app;
        }

        /// <summary>
        /// Adds a middleware that collects headers to be propagated.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);
            return app.UseMiddleware<HeaderPropagationMiddleware>();
        }

        #endregion

        #region Private Helpers

        private sealed class HeaderPropagationStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.UseMiddleware<HeaderPropagationMiddleware>();
                    next(app);
                };
            }
        }

        private static List<IMiddleware> GetOrCreateMiddlewareList(IServiceCollection services)
        {
            // Check if we already set up the collection
            for (int i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == typeof(AgentMiddlewareCollection))
                {
                    // Retrieve the existing instance from the singleton registration
                    if (services[i].ImplementationInstance is AgentMiddlewareCollection existing)
                    {
                        return existing.Middlewares;
                    }
                }
            }

            var collection = new AgentMiddlewareCollection();
            services.AddSingleton(collection);

            // Register the IMiddleware[] that CloudAdapter resolves from DI
            services.AddSingleton<IMiddleware[]>(sp => sp.GetRequiredService<AgentMiddlewareCollection>().Middlewares.ToArray());

            return collection.Middlewares;
        }

        private static List<IInputFileDownloader> GetOrCreateFileDownloaderList(IServiceCollection services)
        {
            for (int i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == typeof(AgentFileDownloaderCollection))
                {
                    if (services[i].ImplementationInstance is AgentFileDownloaderCollection existing)
                    {
                        return existing.Downloaders;
                    }
                }
            }

            var collection = new AgentFileDownloaderCollection();
            services.AddSingleton(collection);

            // Register IList<IInputFileDownloader> that AgentApplicationOptions resolves from DI
            services.AddSingleton<IList<IInputFileDownloader>>(sp =>
            {
                var list = new List<IInputFileDownloader>(sp.GetRequiredService<AgentFileDownloaderCollection>().Downloaders);

                // Resolve deferred downloaders that need DI services
                var deferred = sp.GetService<DeferredAttachmentDownloader>();
                if (deferred != null)
                {
                    list.Add(new AttachmentDownloader(sp.GetRequiredService<IHttpClientFactory>()));
                }

                var deferredM365 = sp.GetService<DeferredM365AttachmentDownloader>();
                if (deferredM365 != null)
                {
                    var options = sp.GetService<M365AttachmentDownloaderOptions>();
                    list.Add(new M365AttachmentDownloader(
                        sp.GetRequiredService<IConnections>(),
                        sp.GetRequiredService<IHttpClientFactory>(),
                        options));
                }

                return list;
            });

            return collection.Downloaders;
        }

        // Internal bookkeeping collections
        internal sealed class AgentMiddlewareCollection
        {
            public List<IMiddleware> Middlewares { get; } = new();
        }

        internal sealed class AgentFileDownloaderCollection
        {
            public List<IInputFileDownloader> Downloaders { get; } = new();
        }

        // Marker classes for deferred downloader registration
        internal sealed class DeferredAttachmentDownloader { }
        internal sealed class DeferredM365AttachmentDownloader { }

        #endregion
    }
}