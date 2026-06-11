// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport
{
    /// <summary>
    /// Thin async wrapper over a <see cref="PipeStream"/> for reading and writing byte buffers.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="NamedPipeTransport"/> class.
    /// </remarks>
    /// <param name="stream">The underlying pipe stream.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    internal sealed class NamedPipeTransport(PipeStream stream) : IAsyncDisposable
    {
        private readonly PipeStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        /// <summary>
        /// Gets a value indicating whether the pipe is connected.
        /// </summary>
        public bool IsConnected => _stream.IsConnected;

        /// <summary>
        /// Read exactly <paramref name="count"/> bytes into the buffer.
        /// Returns false if the pipe disconnects before all bytes are read.
        /// </summary>
        /// <param name="buffer">The buffer to read into. Must be at least <paramref name="count"/> bytes long.</param>
        /// <param name="count">The number of bytes to read. Must be non-negative and not exceed <c>buffer.Length</c>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True if all bytes were read; false if the pipe disconnected.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="count"/> is negative or greater than <c>buffer.Length</c>.
        /// </exception>
        public async Task<bool> ReadExactAsync(Memory<byte> buffer, int count, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length);

            var totalRead = 0;
            while (totalRead < count)
            {
                var read = await _stream.ReadAsync(buffer[totalRead..count], cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                totalRead += read;
            }

            return true;
        }

        /// <summary>
        /// Performs a single read operation, returning up to <paramref name="buffer"/> length bytes.
        /// Unlike <see cref="ReadExactAsync"/>, this does not loop — it returns after one
        /// underlying read. May block until data arrives or the pipe disconnects.
        /// </summary>
        /// <returns>Number of bytes read (0 means pipe disconnected).</returns>
        public async Task<int> ReadSingleAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Write the buffer to the pipe.
        /// </summary>
        /// <param name="buffer">The data to write.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
