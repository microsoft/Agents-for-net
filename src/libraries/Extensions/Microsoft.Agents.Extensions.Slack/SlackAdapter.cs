// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

public class SlackAdapter : CloudAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SlackAdapter(
        IHttpClientFactory httpClientFactory,
        IChannelServiceClientFactory channelServiceClientFactory, 
        IActivityTaskQueue activityTaskQueue, 
        ILogger<CloudAdapter> logger = null, 
        AdapterOptions options = null, 
        IMiddleware[] middlewares = null, 
        IConfiguration config = null) : base(channelServiceClientFactory, activityTaskQueue, logger, options, middlewares, config)
    {
        AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
    {
        return await base.HostResponseAsync(incomingActivity, outActivity, cancellationToken);
    }

    protected override Task RunPipelineAsync(ITurnContext turnContext, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        // make the slack ABS available to the dev during the turn
        turnContext.Services.Set(new SlackApi(_httpClientFactory));

        return base.RunPipelineAsync(turnContext, callback, cancellationToken);
    }
}
