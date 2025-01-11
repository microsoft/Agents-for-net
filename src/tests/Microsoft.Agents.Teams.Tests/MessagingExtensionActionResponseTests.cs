// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Teams.Tests
{
    public class MessagingExtensionActionResponseTests
    {
        [Fact]
        public void MessagingExtensionActionResponseInits()
        {
            var task = new TaskModuleResponseBase("message");
            var composeExtension = new MessagingExtensionResult("list", "message", null, null, "with a personality like sunshine");
            var cacheInfo = new CacheInfo();

            var msgExtActionResponse = new MessagingExtensionActionResponse(task, composeExtension)
            { 
                CacheInfo = cacheInfo
            };

            Assert.NotNull(msgExtActionResponse);
            Assert.IsType<MessagingExtensionActionResponse>(msgExtActionResponse);
            Assert.Equal(task, msgExtActionResponse.Task);
            Assert.Equal(composeExtension, msgExtActionResponse.ComposeExtension);
            Assert.Equal(cacheInfo, msgExtActionResponse.CacheInfo);
        }
        
        [Fact]
        public void MessagingExtensionActionResponseInitsWithNoArgs()
        {
            var msgExtActionResponse = new MessagingExtensionActionResponse();

            Assert.NotNull(msgExtActionResponse);
            Assert.IsType<MessagingExtensionActionResponse>(msgExtActionResponse);
        }
    }
}
