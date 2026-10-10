// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Validates agent configuration during Development startup, before the application accepts requests.
    /// </summary>
    internal sealed class AgentConfigurationValidationHostedService(
        IServiceProvider serviceProvider,
        IHostEnvironment environment) : IHostedLifecycleService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly IHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));

        /// <inheritdoc/>
        public Task StartingAsync(CancellationToken cancellationToken)
        {
            if (_environment.IsDevelopment())
            {
                _serviceProvider.GetRequiredService<AgentApplicationOptions>().Validate();
                _serviceProvider.GetService<HostedActivityServiceOptions>()?.Validate();
                _serviceProvider.GetService<HostedTaskServiceOptions>()?.Validate();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
