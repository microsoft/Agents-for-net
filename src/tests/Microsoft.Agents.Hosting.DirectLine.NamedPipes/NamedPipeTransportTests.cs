// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    public class NamedPipeTransportTests
    {
        [Fact]
        public void Constructor_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NamedPipeTransport(null!));
        }

        [Fact]
        public async Task ReadExactAsync_CountGreaterThanBufferLength_Throws()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In);
            var transport = new NamedPipeTransport(server);

            var buffer = new byte[8];

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await transport.ReadExactAsync(buffer, count: 16));
        }

        [Fact]
        public async Task ReadExactAsync_NegativeCount_Throws()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In);
            var transport = new NamedPipeTransport(server);

            var buffer = new byte[8];

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await transport.ReadExactAsync(buffer, count: -1));
        }

        [Fact]
        public async Task ReadExactAsync_ZeroCount_ReturnsTrueWithoutReading()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In);
            var transport = new NamedPipeTransport(server);

            var buffer = new byte[8];

            var result = await transport.ReadExactAsync(buffer, count: 0);

            Assert.True(result);
        }

        [Fact]
        public async Task ReadExactAsync_ReadsAllRequestedBytes()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);

            var transport = new NamedPipeTransport(server);
            var payload = Encoding.ASCII.GetBytes("hello-pipe!");

            var writeTask = Task.Run(async () =>
            {
                await client.WriteAsync(payload);
                await client.FlushAsync();
            });

            var buffer = new byte[payload.Length];
            var result = await transport.ReadExactAsync(buffer, payload.Length);
            await writeTask;

            Assert.True(result);
            Assert.Equal(payload, buffer);
        }

        [Fact]
        public async Task ReadExactAsync_PipeClosedBeforeAllBytesRead_ReturnsFalse()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using (var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle))
            {
                // Write less than the caller will request, then close.
                await client.WriteAsync(new byte[] { 1, 2, 3 });
                await client.FlushAsync();
            }

            var transport = new NamedPipeTransport(server);
            var buffer = new byte[8];

            var result = await transport.ReadExactAsync(buffer, buffer.Length);

            Assert.False(result);
        }
    }
}
