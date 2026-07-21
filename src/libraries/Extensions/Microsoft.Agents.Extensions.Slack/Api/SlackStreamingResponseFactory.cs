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
    /// The factory is instantiated from dependency injection, so it can consume any registered service.  It reads
    /// optional streaming settings from the <c>Slack:Streaming</c> configuration section (<c>Interval</c> and
    /// <c>InitialDelay</c>, in milliseconds) and applies them to each created <see cref="SlackStreamingResponse"/>.
    /// </remarks>
    [StreamingResponseFactory(Channels.Slack)]
    internal class SlackStreamingResponseFactory : IStreamingResponseFactory
    {
        private const string StreamingSection = "Slack:Streaming";

        private readonly SlackApi _slackApi;
        private readonly IConfiguration _configuration;

        public SlackStreamingResponseFactory(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            _slackApi = new SlackApi(httpClientFactory);
            _configuration = configuration;
        }

        /// <inheritdoc/>
        public IStreamingResponse Create(ITurnContext turnContext)
        {
            var response = new SlackStreamingResponse(turnContext, _slackApi);

            var section = _configuration?.GetSection(StreamingSection);
            if (section != null && section.Exists())
            {
                if (TryGetInt(section, "Interval", out var interval))
                {
                    response.Interval = interval;
                }

                if (TryGetInt(section, "InitialDelay", out var initialDelay))
                {
                    response.InitialDelay = initialDelay;
                }
            }

            return response;
        }

        private static bool TryGetInt(IConfiguration section, string key, out int value)
            => int.TryParse(section[key], out value);
    }
}
