// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    /// <summary>
    /// Creates <see cref="SlackStreamingResponse"/> instances for Slack turns.  Discovered and registered for the
    /// Slack channel automatically on module load via <see cref="StreamingResponseFactoryAttribute"/> - no
    /// <c>services.AddSlack()</c> call is required.
    /// </summary>
    /// <remarks>
    /// The factory is instantiated from dependency injection. Slack uses native batching and rate-limit
    /// pacing rather than the Activity Protocol interval settings.
    /// </remarks>
    [StreamingResponseFactory(Channels.Slack)]
    internal class SlackStreamingResponseFactory : IStreamingResponseFactory
    {
        private readonly SlackApi _slackApi;

        public SlackStreamingResponseFactory(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            _slackApi = new SlackApi(httpClientFactory);
        }

        /// <inheritdoc/>
        public IStreamingResponse Create(ITurnContext turnContext)
        {
            return new SlackStreamingResponse(turnContext, _slackApi);
        }
    }
}
