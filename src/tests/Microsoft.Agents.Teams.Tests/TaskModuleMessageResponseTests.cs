// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Teams.Tests
{
    public class TaskModuleMessageResponseTests
    {
        [Fact]
        public void TaskModuleMessageResponseInits()
        {
            var value = "message value for Teams popup";

            var messageResponse = new TaskModuleMessageResponse(value);

            Assert.NotNull(messageResponse);
            Assert.IsType<TaskModuleMessageResponse>(messageResponse);
            Assert.Equal(value, messageResponse.Value);
            Assert.Equal("message", messageResponse.Type);
        }
        
        [Fact]
        public void TaskModuleMessageResponseInitsWithNoArgs()
        {
            var messageResponse = new TaskModuleMessageResponse();

            Assert.NotNull(messageResponse);
            Assert.IsType<TaskModuleMessageResponse>(messageResponse);
            Assert.Equal("message", messageResponse.Type);
        }
    }
}
