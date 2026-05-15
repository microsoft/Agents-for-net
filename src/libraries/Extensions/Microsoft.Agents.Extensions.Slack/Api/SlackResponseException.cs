using System;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class SlackResponseException : Exception
    {
        public SlackResponseException(string message) : base(message)
        {

        }
    }
}
