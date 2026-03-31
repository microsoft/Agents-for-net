// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions
{
    /// <summary>
    /// Factory methods for constructing <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> objects
    /// returned from message extension handlers.
    /// </summary>
    public static class Response
    {
        /// <summary>
        /// Creates a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> wrapping the given result.
        /// </summary>
        /// <param name="result">The result to include in the response.</param>
        /// <param name="cacheInfo">Optional cache directives for the response.</param>
        /// <returns>A response containing the provided result.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResult(Microsoft.Teams.Api.MessageExtensions.Result result, Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = result,
                CacheInfo = cacheInfo
            };
        }

        /// <summary>
        /// Creates a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> that displays a plain text message to the user.
        /// </summary>
        /// <param name="message">The message text to display.</param>
        /// <returns>A response containing a message-type result with the given text.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResultMessage(string message)
        {
            return WithResult(new Microsoft.Teams.Api.MessageExtensions.Result
            {
                Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Message,
                Text = message
            });
        }
    }

    /// <summary>
    /// Factory methods for constructing <see cref="Task{TResult}"/> of <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/>
    /// for use as the return value of message extension handler methods.
    /// </summary>
    public static class ResponseTask
    {
        /// <summary>
        /// Creates a completed task containing a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> wrapping the given result.
        /// </summary>
        /// <param name="result">The result to include in the response.</param>
        /// <param name="cacheInfo">Optional cache directives for the response.</param>
        /// <returns>A completed task whose result contains the provided result.</returns>
        public static System.Threading.Tasks.Task<Microsoft.Teams.Api.MessageExtensions.Response> WithResult(Microsoft.Teams.Api.MessageExtensions.Result result, Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return Task.FromResult(Response.WithResult(result, cacheInfo));
        }

        /// <summary>
        /// Creates a completed task containing a <see cref="Microsoft.Teams.Api.MessageExtensions.Response"/> that displays a plain text message to the user.
        /// </summary>
        /// <param name="message">The message text to display.</param>
        /// <returns>A completed task whose result contains a message-type response with the given text.</returns>
        public static Task<Microsoft.Teams.Api.MessageExtensions.Response> WithResultMessage(string message)
        {
            return Task.FromResult(Response.WithResultMessage(message));
        }
    }
}
