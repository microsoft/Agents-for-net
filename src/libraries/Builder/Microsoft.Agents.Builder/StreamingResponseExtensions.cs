// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder;

/// <summary>
/// Provides optional final-delivery capabilities for the SDK streaming response.
/// </summary>
public static class StreamingResponseExtensions
{
    /// <summary>
    /// Ends the stream using a custom sender for its fully composed final activity.
    /// </summary>
    /// <remarks>
    /// The sender receives the final text, attachments, citations, labels, and applicable streaming
    /// identifiers. It owns final delivery and recovery; exceptions propagate to the ending caller.
    /// Intermediate sends and timeout checkpoints retain their normal behavior. An empty response
    /// does not invoke the sender. The sender is not retained for subsequent streams.
    /// Use <see cref="Microsoft.Agents.Builder.IStreamingResponse.EndStreamAsync(System.Threading.CancellationToken)"/>
    /// for the default delivery and recovery behavior.
    /// </remarks>
    /// <param name="response">The SDK streaming response to end.</param>
    /// <param name="finalActivitySender">The asynchronous final-activity sender.</param>
    /// <param name="cancellationToken">The cancellation token for final delivery.</param>
    /// <returns>The result of ending the stream.</returns>
    /// <exception cref="System.NotSupportedException">The response is not the SDK streaming writer.</exception>
    public static Task<StreamingResponseResult> EndStreamAsync(
        this IStreamingResponse response,
        Func<IActivity, CancellationToken, Task<ResourceResponse>> finalActivitySender,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(response, nameof(response));
        AssertionHelpers.ThrowIfNull(finalActivitySender, nameof(finalActivitySender));

        // COMPAT(next-major): Preserve IStreamingResponse for existing custom writers.
        // Consider a shared final-delivery capability contract, migrating custom writers
        // and this built-in-only overload without relying on the concrete SDK writer.
        if (response is not StreamingResponse streamingResponse)
        {
            throw new NotSupportedException("This streaming response does not support custom final delivery.");
        }

        return streamingResponse.EndStreamAsync(finalActivitySender, cancellationToken);
    }
}
