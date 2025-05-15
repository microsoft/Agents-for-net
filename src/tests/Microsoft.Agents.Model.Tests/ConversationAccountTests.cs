// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ConversationAccountTests
    {
        [Fact]
        public void ConversationAccount_RoundTrip()
        {
            var jsonIn = "{\"isGroup\":true,\"conversationType\":\"convType\",\"tenantId\":\"tenant_id\",\"id\":\"id\",\"name\":\"convName\",\"aadObjectId\":\"aadObject_id\",\"role\":\"convRole\",\"custom1\":\"custom1Value\",\"custom2\":\"custom2Value\"}";

            var conversationAccountIn = ProtocolJsonSerializer.ToObject<ConversationAccount>(jsonIn);

            Assert.Equal(2, conversationAccountIn.Properties.Count);

            var jsonOut = ProtocolJsonSerializer.ToJson(conversationAccountIn);
            Assert.Equal(jsonIn, jsonOut);
        }
    }
}
