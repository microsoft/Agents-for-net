// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Testing.Http
{
    internal static class HttpRequestBody
    {
        internal static async Task<byte[]> ReadAsync(HttpRequestMessage request, int limit, CancellationToken cancellationToken)
        {
            var content = request.Content;
            if (content == null)
            {
                return [];
            }

            if (content.Headers.ContentLength > limit)
            {
                throw new BodyLimitException();
            }

            using var buffer = new BoundedBuffer(limit, cancellationToken);
            if (content.GetType() == typeof(StreamContent))
            {
                // StreamContent exposes its stream without HttpContent's default unbounded buffering.
#if NET8_0_OR_GREATER
                var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                var source = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                var chunk = new byte[Math.Min(8192L, (long)limit + 1)];
                int read;
                do
                {
                    var count = (int)Math.Min(chunk.Length, (long)limit - buffer.Length + 1);
#if NET8_0_OR_GREATER
                    read = await source.ReadAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
#else
                    read = await source.ReadAsync(chunk, 0, count, cancellationToken).ConfigureAwait(false);
#endif
                    buffer.Write(chunk, 0, read);
                }
                while (read != 0);
            }
            else
            {
#if NET8_0_OR_GREATER
                await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
                await content.CopyToAsync(buffer).ConfigureAwait(false);
#endif
            }

            cancellationToken.ThrowIfCancellationRequested();
            var bytes = buffer.ToArray();
            request.Content = new ReplayContent(bytes, content);
            return bytes;
        }

        internal sealed class BodyLimitException : Exception
        {
        }

        private sealed class BoundedBuffer(int limit, CancellationToken cancellationToken) : Stream
        {
            private readonly MemoryStream _buffer = new();

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _buffer.Length;
            public override long Position { get => _buffer.Position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            internal byte[] ToArray() => _buffer.ToArray();

            public override void Write(byte[] buffer, int offset, int count)
            {
                CheckLength(count);
                _buffer.Write(buffer, offset, count);
            }

            public override void WriteByte(byte value)
            {
                CheckLength(1);
                _buffer.WriteByte(value);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Write(buffer, offset, count);
                return Task.CompletedTask;
            }

#if NET8_0_OR_GREATER
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                CheckLength(buffer.Length);
                _buffer.Write(buffer);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                Write(buffer.Span);
                return default;
            }
#endif

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _buffer.Dispose();
                }

                base.Dispose(disposing);
            }

            private void CheckLength(int count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (count > limit - Length)
                {
                    throw new BodyLimitException();
                }
            }
        }

        private sealed class ReplayContent : HttpContent
        {
            private readonly byte[] _bytes;
            private HttpContent _original;

            internal ReplayContent(byte[] bytes, HttpContent original)
            {
                _bytes = bytes;
                _original = original;
                foreach (var header in original.Headers)
                {
                    Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _bytes.Length;
                return true;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => stream.WriteAsync(_bytes, 0, _bytes.Length);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Interlocked.Exchange(ref _original, null)?.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
