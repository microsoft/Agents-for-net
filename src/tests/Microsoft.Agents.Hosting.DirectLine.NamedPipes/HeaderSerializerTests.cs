// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    public class HeaderSerializerTests
    {
        [Fact]
        public void Serialize_Deserialize_RoundTrip()
        {
            var original = new Header
            {
                Type = PayloadTypes.Request,
                PayloadLength = 1234,
                Id = Guid.Parse("12345678-1234-1234-1234-123456789012"),
                End = true
            };

            var bytes = HeaderSerializer.Serialize(original);
            var deserialized = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(original.Type, deserialized.Type);
            Assert.Equal(original.PayloadLength, deserialized.PayloadLength);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.End, deserialized.End);
        }

        [Fact]
        public void Serialize_ProducesCorrectSize()
        {
            var header = new Header
            {
                Type = PayloadTypes.Response,
                PayloadLength = 0,
                Id = Guid.NewGuid(),
                End = false
            };

            var bytes = HeaderSerializer.Serialize(header);

            Assert.Equal(HeaderSerializer.HeaderSize, bytes.Length);
        }

        [Fact]
        public void Serialize_EndFalse_HasZeroByte()
        {
            var header = new Header
            {
                Type = PayloadTypes.Stream,
                PayloadLength = 42,
                Id = Guid.NewGuid(),
                End = false
            };

            var bytes = HeaderSerializer.Serialize(header);

            Assert.Equal((byte)'0', bytes[46]);
        }

        [Fact]
        public void Serialize_EndTrue_HasOneByte()
        {
            var header = new Header
            {
                Type = PayloadTypes.Stream,
                PayloadLength = 42,
                Id = Guid.NewGuid(),
                End = true
            };

            var bytes = HeaderSerializer.Serialize(header);

            Assert.Equal((byte)'1', bytes[46]);
        }

        [Fact]
        public void Deserialize_InvalidLength_ThrowsFormatException()
        {
            var bytes = new byte[HeaderSerializer.HeaderSize];
            bytes[0] = (byte)'A';
            bytes[1] = (byte)'.';
            // Invalid length chars
            bytes[2] = (byte)'X';
            bytes[3] = (byte)'X';
            bytes[4] = (byte)'X';
            bytes[5] = (byte)'X';
            bytes[6] = (byte)'X';
            bytes[7] = (byte)'X';

            Assert.Throws<FormatException>(() => HeaderSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Deserialize_BufferTooSmall_ThrowsArgumentException()
        {
            var bytes = new byte[10];

            Assert.Throws<ArgumentException>(() => HeaderSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Serialize_BufferTooSmall_ThrowsArgumentException()
        {
            var header = new Header
            {
                Type = PayloadTypes.Request,
                PayloadLength = 0,
                Id = Guid.NewGuid(),
                End = true
            };

            var buffer = new byte[10];

            Assert.Throws<ArgumentException>(() => HeaderSerializer.Serialize(header, buffer));
        }

        [Theory]
        [InlineData(PayloadTypes.Request)]
        [InlineData(PayloadTypes.Response)]
        [InlineData(PayloadTypes.Stream)]
        [InlineData(PayloadTypes.CancelAll)]
        [InlineData(PayloadTypes.CancelStream)]
        public void Serialize_AllPayloadTypes_RoundTrip(char payloadType)
        {
            var original = new Header
            {
                Type = payloadType,
                PayloadLength = 999999,
                Id = Guid.NewGuid(),
                End = true
            };

            var bytes = HeaderSerializer.Serialize(original);
            var deserialized = HeaderSerializer.Deserialize(bytes);

            Assert.Equal(original.Type, deserialized.Type);
            Assert.Equal(original.PayloadLength, deserialized.PayloadLength);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.End, deserialized.End);
        }
    }
}
