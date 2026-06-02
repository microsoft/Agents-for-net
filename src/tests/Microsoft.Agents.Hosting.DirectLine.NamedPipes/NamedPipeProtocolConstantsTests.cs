// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Regression tests that lock the internal protocol limits to their wire-protocol
    /// values. These constants are load-bearing for interop with DirectLine and Bot.Streaming
    /// peers, so changing them silently would be a behavioral incompatibility.
    /// </summary>
    public class NamedPipeProtocolConstantsTests
    {
        [Fact]
        public void MaxPayloadSize_MatchesBotStreamingMaxLength()
        {
            // Bot.Streaming TransportConstants.MaxLength = 999_999, which is also the
            // hard ceiling implied by the 6-ASCII-digit Header.PayloadLength field.
            var value = GetPrivateConstInt32(typeof(NamedPipeProtocol), "MaxPayloadSize");
            Assert.Equal(999_999, value);
        }

        [Fact]
        public void MaxStreamBuffers_IsBounded()
        {
            // Must be a positive bound to prevent unbounded growth from a misbehaving peer.
            var value = GetPrivateConstInt32(typeof(NamedPipeProtocol), "MaxStreamBuffers");
            Assert.InRange(value, 1, 10_000);
        }

        private static int GetPrivateConstInt32(System.Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new System.InvalidOperationException($"Constant '{name}' not found on {type.FullName}.");
            return (int)field.GetRawConstantValue();
        }
    }
}
