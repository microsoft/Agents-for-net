// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Tests that cover the wire-protocol boundary conditions enforced by
    /// <see cref="HeaderSerializer"/>. The on-wire payload-length field is six
    /// ASCII digits, which mathematically caps any single frame at 999,999 bytes.
    /// </summary>
    public class HeaderSerializerBoundaryTests
    {
        /// <summary>
        /// The maximum payload length expressible by the 6-digit ASCII length field.
        /// Mirrors <c>Microsoft.Bot.Streaming.Transport.TransportConstants.MaxLength</c>.
        /// </summary>
        private const int MaxPayloadLength = 999_999;

        [Fact]
        public void Serialize_PayloadLengthZero_RoundTrips()
        {
            var original = new Header
            {
                Type = PayloadTypes.Request,
                PayloadLength = 0,
                Id = Guid.NewGuid(),
                End = true
            };

            var bytes = HeaderSerializer.Serialize(original);
            var deserialized = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(0, deserialized.PayloadLength);
        }

        [Fact]
        public void Serialize_PayloadLengthAtMax_RoundTrips()
        {
            var original = new Header
            {
                Type = PayloadTypes.Stream,
                PayloadLength = MaxPayloadLength,
                Id = Guid.NewGuid(),
                End = false
            };

            var bytes = HeaderSerializer.Serialize(original);
            var deserialized = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(MaxPayloadLength, deserialized.PayloadLength);
        }

        [Fact]
        public void Serialize_PayloadLengthAtMax_LengthFieldIsExactlySixDigits()
        {
            var header = new Header
            {
                Type = PayloadTypes.Stream,
                PayloadLength = MaxPayloadLength,
                Id = Guid.NewGuid(),
                End = false
            };

            var bytes = HeaderSerializer.Serialize(header);

            // Bytes 2..8 must read "999999".
            var lengthField = Encoding.ASCII.GetString(bytes, 2, 6);
            Assert.Equal("999999", lengthField);
        }

        [Fact]
        public void Serialize_PayloadLengthExceedsMax_Throws()
        {
            // 1_000_000 cannot be encoded in 6 ASCII digits — the serializer must reject it.
            var header = new Header
            {
                Type = PayloadTypes.Request,
                PayloadLength = MaxPayloadLength + 1,
                Id = Guid.NewGuid(),
                End = true
            };

            Assert.ThrowsAny<ArgumentException>(() => HeaderSerializer.Serialize(header));
        }

        [Fact]
        public void Serialize_NegativePayloadLength_Throws()
        {
            // Negative lengths produce a 7-character string ("-00001") via D6 formatting
            // and cannot fit the 6-byte length field on the wire.
            var header = new Header
            {
                Type = PayloadTypes.Request,
                PayloadLength = -1,
                Id = Guid.NewGuid(),
                End = true
            };

            Assert.ThrowsAny<ArgumentException>(() => HeaderSerializer.Serialize(header));
        }

        [Fact]
        public void Deserialize_LengthWithLeadingZeros_Parses()
        {
            // "000042" should parse as 42.
            var bytes = BuildHeaderBytes(
                type: PayloadTypes.Response,
                lengthAscii: "000042",
                idStr: "12345678-1234-1234-1234-123456789012",
                end: '1');

            var header = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(42, header.PayloadLength);
        }

        [Fact]
        public void Deserialize_LengthAtMaxAscii_Parses()
        {
            // Confirm a peer-sent header at the absolute ceiling deserializes.
            var bytes = BuildHeaderBytes(
                type: PayloadTypes.Stream,
                lengthAscii: "999999",
                idStr: Guid.NewGuid().ToString("D"),
                end: '1');

            var header = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(MaxPayloadLength, header.PayloadLength);
        }

        [Fact]
        public void Deserialize_InvalidGuid_ThrowsFormatException()
        {
            var bytes = BuildHeaderBytes(
                type: PayloadTypes.Request,
                lengthAscii: "000010",
                idStr: "not-a-guid--------------------------",
                end: '0');

            Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Deserialize_EndCharZero_TreatedAsFalse()
        {
            // '0' indicates a non-terminal frame for this payload-id; must deserialize to End=false.
            var bytes = BuildHeaderBytes(
                type: PayloadTypes.Stream,
                lengthAscii: "000010",
                idStr: Guid.NewGuid().ToString("D"),
                end: '0');

            var header = HeaderSerializer.Deserialize(bytes);

            Assert.False(header.End);
        }

        [Fact]
        public void Serialize_HeaderTerminatorIsNewline()
        {
            var bytes = HeaderSerializer.Serialize(new Header
            {
                Type = PayloadTypes.Request,
                PayloadLength = 0,
                Id = Guid.NewGuid(),
                End = true
            });

            Assert.Equal((byte)'\n', bytes[HeaderSerializer.HeaderSize - 1]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1_000)]
        [InlineData(10_000)]
        [InlineData(100_000)]
        [InlineData(999_998)]
        [InlineData(999_999)]
        public void Serialize_VariousLengths_RoundTrip(int payloadLength)
        {
            var original = new Header
            {
                Type = PayloadTypes.Stream,
                PayloadLength = payloadLength,
                Id = Guid.NewGuid(),
                End = false
            };

            var bytes = HeaderSerializer.Serialize(original);
            var deserialized = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(payloadLength, deserialized.PayloadLength);
        }

        [Fact]
        public void Deserialize_TypeDelimiterMalformed_ThrowsFormatException()
        {
            var bytes = BuildHeaderBytes(PayloadTypes.Request, "000000", Guid.NewGuid().ToString("D"), '1');
            bytes[1] = (byte)'!'; // expected '.'

            var ex = Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
            Assert.Contains("offset 1", ex.Message);
        }

        [Fact]
        public void Deserialize_LengthDelimiterMalformed_ThrowsFormatException()
        {
            var bytes = BuildHeaderBytes(PayloadTypes.Request, "000000", Guid.NewGuid().ToString("D"), '1');
            bytes[8] = (byte)'X';

            var ex = Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
            Assert.Contains("offset 8", ex.Message);
        }

        [Fact]
        public void Deserialize_IdDelimiterMalformed_ThrowsFormatException()
        {
            var bytes = BuildHeaderBytes(PayloadTypes.Request, "000000", Guid.NewGuid().ToString("D"), '1');
            bytes[45] = (byte)' ';

            var ex = Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
            Assert.Contains("offset 45", ex.Message);
        }

        [Fact]
        public void Deserialize_EndCharNotZeroOrOne_ThrowsFormatException()
        {
            // Strict: only '0' or '1' are valid. Anything else indicates framing desync.
            var bytes = BuildHeaderBytes(PayloadTypes.Stream, "000000", Guid.NewGuid().ToString("D"), '2');

            var ex = Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
            Assert.Contains("offset 46", ex.Message);
        }

        [Fact]
        public void Deserialize_TerminatorNotNewline_ThrowsFormatException()
        {
            var bytes = BuildHeaderBytes(PayloadTypes.Request, "000000", Guid.NewGuid().ToString("D"), '1');
            bytes[47] = (byte)'\r'; // expected '\n'

            var ex = Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
            Assert.Contains("offset 47", ex.Message);
        }

        private static byte[] BuildHeaderBytes(char type, string lengthAscii, string idStr, char end)
        {
            if (lengthAscii.Length != 6)
            {
                throw new ArgumentException("lengthAscii must be exactly 6 characters.", nameof(lengthAscii));
            }

            if (idStr.Length != 36)
            {
                throw new ArgumentException("idStr must be exactly 36 characters.", nameof(idStr));
            }

            var buffer = new byte[HeaderSerializer.HeaderSize];
            buffer[0] = (byte)type;
            buffer[1] = (byte)'.';
            Encoding.ASCII.GetBytes(lengthAscii, 0, 6, buffer, 2);
            buffer[8] = (byte)'.';
            Encoding.ASCII.GetBytes(idStr, 0, 36, buffer, 9);
            buffer[45] = (byte)'.';
            buffer[46] = (byte)end;
            buffer[47] = (byte)'\n';
            return buffer;
        }
    }
}
