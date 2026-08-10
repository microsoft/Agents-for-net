// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Slack.Api;
using Moq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackStreamingResponseTests
{
    [Fact]
    public async Task ResetAsync_RestoresConfiguredDefaults()
    {
        var response = new SlackStreamingResponse(
            new Mock<ITurnContext>().Object,
            new SlackApi(new Mock<IHttpClientFactory>().Object));
        response.ConfigureDefaults(interval: 321, initialDelay: 123);

        response.Interval = 1;
        response.InitialDelay = 1;
        response.FeedbackLoopEnabled = true;

        await response.ResetAsync();

        Assert.True(response.IsStreamingChannel);
        Assert.Equal(321, response.Interval);
        Assert.Equal(123, response.InitialDelay);
        Assert.False(response.FeedbackLoopEnabled);
        Assert.Null(response.StreamId);
    }
}
