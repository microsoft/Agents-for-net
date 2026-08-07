// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Creates a channel-specific <see cref="IStreamingResponse"/> for a turn.
    /// </summary>
    /// <remarks>
    /// Decorate an implementation with <see cref="StreamingResponseFactoryAttribute"/> to register it for a
    /// channel; it is then discovered automatically on module load (no service-collection call is required).  The
    /// adapter resolves the factory by <see cref="ITurnContext.Activity"/>'s channel id, instantiates it from the
    /// service provider (so its constructor dependencies are injected), and assigns the resulting
    /// <see cref="IStreamingResponse"/> to the turn before the pipeline runs.  When no factory is registered for a
    /// channel, the default <see cref="StreamingResponse"/> is used.
    /// </remarks>
    public interface IStreamingResponseFactory
    {
        /// <summary>
        /// Creates an <see cref="IStreamingResponse"/> for the given turn.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        /// <returns>A channel-specific <see cref="IStreamingResponse"/>.</returns>
        IStreamingResponse Create(ITurnContext turnContext);
    }
}
