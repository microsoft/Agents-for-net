// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    public class NamedPipeConnectionTests
    {
        [Fact]
        public void Constructor_NullPipeName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NamedPipeConnection(null!, NullLogger.Instance));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        public void Constructor_EmptyOrWhitespacePipeName_Throws(string pipeName)
        {
            Assert.Throws<ArgumentException>(() => new NamedPipeConnection(pipeName, NullLogger.Instance));
        }

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NamedPipeConnection("bfv4.pipes", null!));
        }

        [Fact]
        public async System.Threading.Tasks.Task Constructor_ValidArguments_DoesNotThrow()
        {
            var connection = new NamedPipeConnection("bfv4.pipes", NullLogger.Instance);
            await connection.DisposeAsync();
        }
    }
}
