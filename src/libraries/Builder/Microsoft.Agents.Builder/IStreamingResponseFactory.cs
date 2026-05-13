// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Creates an <see cref="IStreamingResponse"/> implementation for a specific channel.
    /// Register implementations via <c>services.AddStreamingResponseFactory&lt;TFactory&gt;("channelId")</c>.
    /// </summary>
    public interface IStreamingResponseFactory
    {
        /// <summary>Creates a streaming response for the given turn context.</summary>
        IStreamingResponse Create(ITurnContext turnContext);
    }
}
