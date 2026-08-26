// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class HostedActivityServiceOptionsTests
    {
        [Fact]
        public void Constructor_WithoutSection_UsesDefaultShutdownTimeout()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = new HostedActivityServiceOptions(configuration);

            Assert.Equal(60, options.ShutdownTimeoutSeconds);
            Assert.True(options.UseScopedServices);
        }

        [Fact]
        public void Constructor_BindsHostedActivityServiceOptionsSection()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["HostedActivityServiceOptions:ShutdownTimeoutSeconds"] = "17",
                    ["HostedActivityServiceOptions:UseScopedServices"] = "false"
                })
                .Build();

            var options = new HostedActivityServiceOptions(configuration);

            Assert.Equal(17, options.ShutdownTimeoutSeconds);
            Assert.False(options.UseScopedServices);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new HostedActivityServiceOptions(null));
        }
    }
}
