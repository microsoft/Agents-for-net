// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Indicates what action StreamingResponseBase should take after HandleSendErrorAsync returns.
    /// </summary>
    public enum StreamErrorAction
    {
        /// <summary>Ignore the error and keep streaming.</summary>
        Continue,

        /// <summary>
        /// Set IsStreamingChannel = false, stop the timer. FinalizeStreamAsync is still called.
        /// Useful for channels that return "streaming not supported" errors at runtime.
        /// </summary>
        FallbackToNonStreaming,

        /// <summary>Stop the stream and return StreamingResponseResult.UserCancelled.</summary>
        Cancel,
    }
}
