// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions
{
    public static class Response
    {
        public static Microsoft.Teams.Api.MessageExtensions.Response WithResult(Microsoft.Teams.Api.MessageExtensions.Result result, Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = result,
                CacheInfo = cacheInfo
            };
        }

        public static Microsoft.Teams.Api.MessageExtensions.Response WithResultMessage(string message)
        {
            return WithResult(new Microsoft.Teams.Api.MessageExtensions.Result
            {
                Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Message,
                Text = message
            });
        }
    }

    public static class ResponseTask
    {
        public static System.Threading.Tasks.Task<Microsoft.Teams.Api.MessageExtensions.Response> WithResult(Microsoft.Teams.Api.MessageExtensions.Result result, Microsoft.Teams.Api.CacheInfo? cacheInfo = null)
        {
            return Task.FromResult(Response.WithResult(result, cacheInfo));
        }

        public static Task<Microsoft.Teams.Api.MessageExtensions.Response> WithResultMessage(string message)
        {
            return Task.FromResult(Response.WithResultMessage(message));
        }
    }
}
