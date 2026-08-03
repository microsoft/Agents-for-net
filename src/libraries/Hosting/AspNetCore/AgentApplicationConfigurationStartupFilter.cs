// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Validates the AgentApplication configuration while the host is starting.
    /// </summary>
    /// <remarks>
    /// <para><see cref="AgentApplicationOptions"/> is registered as a singleton and DI doesn't create it until the
    /// Agent handles its first Activity.  Configuration errors, such as a <c>DefaultHandlerName</c> that doesn't name
    /// a defined handler, would therefore first appear as a failed conversation.  This runs the same validation
    /// before the server starts accepting requests, so the host fails to start instead.</para>
    /// <para>An <see cref="IStartupFilter"/> is used rather than an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
    /// because startup filters run while the request pipeline is being built, which is before the server is listening.</para>
    /// </remarks>
    internal sealed class AgentApplicationConfigurationStartupFilter : IStartupFilter
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProviderIsService _serviceProviderIsService;

        public AgentApplicationConfigurationStartupFilter(IConfiguration configuration, IServiceProviderIsService serviceProviderIsService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _serviceProviderIsService = serviceProviderIsService ?? throw new ArgumentNullException(nameof(serviceProviderIsService));
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            // AgentApplicationOptions prefers a DI'd UserAuthorizationOptions over the configuration section, and
            // the registered instance isn't created here to check it.  Config validation is skipped in that case.
            if (!_serviceProviderIsService.IsService(typeof(UserAuthorizationOptions)))
            {
                AgentApplicationConfigurationValidator.Validate(_configuration);
            }

            return next;
        }
    }
}
