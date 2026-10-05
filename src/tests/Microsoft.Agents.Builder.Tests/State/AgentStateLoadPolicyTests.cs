// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.State
{
    public class AgentStateLoadPolicyTests
    {
        [Fact]
        public async Task LoadAsync_WhenPolicyRejectsScope_DoesNotLoadState()
        {
            var state = new UserState(new MemoryStorage());
            var turnContext = new TurnContext(new TestAdapter(), new Activity
            {
                ChannelId = "test",
                Conversation = new ConversationAccount { Id = "conversation" },
                From = new ChannelAccount { Id = "user" },
            });
            turnContext.Services.Set<IAgentStateLoadPolicy>(new RejectUserStateLoadPolicy());

            await state.LoadAsync(turnContext);

            Assert.False(state.IsLoaded());
        }

        private sealed class RejectUserStateLoadPolicy : IAgentStateLoadPolicy
        {
            public bool ShouldLoad(ITurnContext turnContext, IAgentState agentState)
            {
                return agentState.Name != UserState.ScopeName;
            }
        }
    }
}
