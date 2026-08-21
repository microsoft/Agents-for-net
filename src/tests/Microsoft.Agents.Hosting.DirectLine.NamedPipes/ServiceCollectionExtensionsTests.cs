// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddAgentNamedPipeTransport_NoPipeName_DoesNotOverrideConfiguration()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration["NamedPipe:PipeName"] = "configured.pipe";

            builder.AddAgentNamedPipeTransport();

            Assert.Equal("configured.pipe", builder.Configuration["NamedPipe:PipeName"]);
        }

        [Fact]
        public void AddAgentNamedPipeTransport_WithPipeName_OverridesConfiguration()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration["NamedPipe:PipeName"] = "configured.pipe";

            builder.AddAgentNamedPipeTransport("explicit.pipe");

            Assert.Equal("explicit.pipe", builder.Configuration["NamedPipe:PipeName"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddAgentNamedPipeTransport_WithInvalidPipeName_Throws(string pipeName)
        {
            var builder = Host.CreateApplicationBuilder();

            Assert.ThrowsAny<ArgumentException>(() => builder.AddAgentNamedPipeTransport(pipeName));
        }
    }
}
