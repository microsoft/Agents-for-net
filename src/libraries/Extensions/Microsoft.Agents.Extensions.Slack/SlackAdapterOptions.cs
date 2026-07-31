// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Slack
{
    /// <summary>
    /// Configuration for the <see cref="SlackAdapter"/> when receiving Slack traffic directly
    /// (Events API + Interactivity) instead of through Azure Bot Service.
    /// </summary>
    /// <remarks>
    /// Bind these from an application configuration section (default <c>"Slack"</c>). All values are
    /// obtained from the Slack app configuration at https://api.slack.com/apps.
    /// </remarks>
    public class SlackAdapterOptions
    {
        /// <summary>
        /// The Slack bot OAuth token (starts with <c>xoxb-</c>) used to call the Slack Web API when
        /// sending replies. Found under "OAuth &amp; Permissions".
        /// </summary>
        public string BotToken { get; set; }

        /// <summary>
        /// The Slack app signing secret used to verify that inbound requests originate from Slack.
        /// Found under "Basic Information". When empty, signature verification is skipped (not
        /// recommended outside local development).
        /// </summary>
        public string SigningSecret { get; set; }

        /// <summary>
        /// The Slack user id of the bot (starts with <c>U</c> or <c>B</c>). Used as the Activity
        /// recipient and to ignore the bot's own messages so it does not reply to itself.
        /// </summary>
        public string BotUserId { get; set; }

        /// <summary>
        /// The Slack application id (starts with <c>A</c>). Optional; recorded on the conversation id.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Maximum age, in seconds, of an inbound request (per the <c>X-Slack-Request-Timestamp</c>
        /// header) before it is rejected as a potential replay. Defaults to 300 (5 minutes).
        /// </summary>
        public int RequestMaxAgeSeconds { get; set; } = 300;
    }
}
