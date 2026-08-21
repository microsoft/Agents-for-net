// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Boundary condition tests for <see cref="NamedPipeTransport"/>:
    /// cancellation, pipe closure during read/write, dispose semantics,
    /// and ReadSingleAsync behavior.
    /// </summary>
    public class NamedPipeTransportBoundaryTests
    {
        [Fact]
        public async Task ReadExactAsync_CancellationDuringRead_ThrowsOperationCanceled()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);
            var transport = new NamedPipeTransport(server);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var buffer = new byte[100];

            // No data written to client — read blocks until cancelled.
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await transport.ReadExactAsync(buffer, 100, cts.Token));
        }

        [Fact]
        public async Task ReadSingleAsync_ReturnsPartialData_WhenLessThanBufferAvailable()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);
            var transport = new NamedPipeTransport(server);

            // Write 5 bytes, but request up to 100.
            await client.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });
            await client.FlushAsync();

            var buffer = new byte[100];
            var read = await transport.ReadSingleAsync(buffer);

            Assert.True(read >= 1 && read <= 5);
        }

        [Fact]
        public async Task ReadSingleAsync_PipeClosed_ReturnsZero()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);
            client.Dispose(); // Close the write end.
            server.DisposeLocalCopyOfClientHandle();

            var transport = new NamedPipeTransport(server);
            var buffer = new byte[100];
            var read = await transport.ReadSingleAsync(buffer);

            Assert.Equal(0, read);
        }

        [Fact]
        public async Task ReadSingleAsync_CancellationBeforeData_ThrowsOperationCanceled()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);
            var transport = new NamedPipeTransport(server);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var buffer = new byte[100];

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await transport.ReadSingleAsync(buffer, cts.Token));
        }

        [Fact]
        public async Task WriteAsync_OnDisposedPipe_Throws()
        {
            var server = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            var transport = new NamedPipeTransport(server);
            server.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await transport.WriteAsync(new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public async Task DisposeAsync_CanBeCalledMultipleTimes()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In);
            var transport = new NamedPipeTransport(server);

            await transport.DisposeAsync();
            // Should not throw.
            await transport.DisposeAsync();
        }

        [Fact]
        public async Task ReadExactAsync_ZeroReturnMidway_ReturnsFalse()
        {
            // Write partial data then close — simulates pipe broken mid-transfer.
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);

            await client.WriteAsync(new byte[] { 10, 20 });
            await client.FlushAsync();
            client.Dispose();
            server.DisposeLocalCopyOfClientHandle();

            var transport = new NamedPipeTransport(server);
            var buffer = new byte[10];

            // Request 10 bytes but only 2 available before EOF.
            var result = await transport.ReadExactAsync(buffer, 10);
            Assert.False(result);
            // First 2 bytes should have been written.
            Assert.Equal(10, buffer[0]);
            Assert.Equal(20, buffer[1]);
        }

        [Fact]
        public void IsConnected_ReflectsPipeState()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In);
            var transport = new NamedPipeTransport(server);

            // Anonymous pipes are always "connected" (no connect/disconnect semantics).
            Assert.True(transport.IsConnected);
        }

        [Fact]
        public async Task ReadExactAsync_ExactBufferSize_ReturnsTrue()
        {
            using var server = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            using var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);
            var transport = new NamedPipeTransport(server);

            var data = new byte[256];
            new Random(42).NextBytes(data);
            await client.WriteAsync(data);
            await client.FlushAsync();

            var buffer = new byte[256];
            var result = await transport.ReadExactAsync(buffer, 256);

            Assert.True(result);
            Assert.Equal(data, buffer);
        }
    }
}
