// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests.BackgroundTaskService
{
    public class HostedTaskServiceOptionsTests
    {
        [Fact]
        public void Constructor_WithoutSection_UsesDefaultShutdownTimeout()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = new HostedTaskServiceOptions(configuration);

            Assert.Equal(60, options.ShutdownTimeoutSeconds);
        }

        [Fact]
        public void Constructor_BindsHostedTaskServiceOptionsSection()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["HostedTaskServiceOptions:ShutdownTimeoutSeconds"] = "17"
                })
                .Build();

            var options = new HostedTaskServiceOptions(configuration);

            Assert.Equal(17, options.ShutdownTimeoutSeconds);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new HostedTaskServiceOptions(null));
        }
    }
}
