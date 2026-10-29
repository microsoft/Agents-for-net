using System;
using System.Net;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class SlackResponseException : Exception
    {
        public SlackResponseException(string message) : base(message)
        {

        }

        internal SlackResponseException(string method, HttpStatusCode statusCode, string errorCode, TimeSpan? retryAfter, string response)
            : base($"Slack API error on {method} (HTTP {(int)statusCode}):\n{response}")
        {
            Method = method;
            StatusCode = statusCode;
            ErrorCode = errorCode;
            RetryAfter = retryAfter;
        }

        public string Method { get; }
        public HttpStatusCode? StatusCode { get; }
        public string ErrorCode { get; }
        public TimeSpan? RetryAfter { get; }
    }
}
