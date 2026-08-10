// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.State;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Applies Microsoft Teams streaming defaults, error handling, and final-message metadata.
    /// </summary>
    internal class TeamsStreamingResponse : StreamingResponse
    {
        private const string TeamsStreamCancelled = "ContentStreamNotAllowed";
        private const string TeamsStreamTimedOut = "Content stream finished due to exceeded streaming time.";
        private const string BadArgument = "BadArgument";
        private const string TeamsStreamNotAllowed = "streaming api is not enabled";

        private bool _streamTimedOut;

        public TeamsStreamingResponse(TurnContext turnContext)
            : base(turnContext)
        {
            SetDefaults(turnContext);
        }

        private void SetDefaults(TurnContext turnContext)
        {
            Interval = 0;
            IsStreamingChannel = false;
            StreamId = null;

            if (string.Equals(
                DeliveryModes.ExpectReplies,
                turnContext.Activity.DeliveryMode,
                StringComparison.OrdinalIgnoreCase))
            {
                IsStreamingChannel = false;
                return;
            }
            Interval = 1000;
            IsStreamingChannel = true;
            IsStreamingChannel = true;
        }

        protected override async Task<StreamErrorAction> HandleSendErrorAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is ErrorResponseException errorResponse)
            {
                if (TeamsStreamCancelled.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase))
                {
                    if (TeamsStreamTimedOut.Equals(errorResponse.Body?.Error?.Message, StringComparison.OrdinalIgnoreCase))
                    {
                        Context.Adapter?.Logger?.LogWarning(
                            "Client canceled due to exceeded allowed streaming time. {ErrorCode} - {ErrorMessage}",
                            errorResponse.Body?.Error?.Code,
                            errorResponse.Body?.Error?.Message);

                        _streamTimedOut = true;
                        await UpdateActivityAsync(CreateStreamTimedOutMessage(), cancellationToken).ConfigureAwait(false);
                        return StreamErrorAction.FallbackToNonStreaming;
                    }

                    Context.Adapter?.Logger?.LogWarning("User canceled stream on the client side.");
                    System.Diagnostics.Trace.WriteLine("User canceled stream on the client side.");
                    UserCancelledStream = true;
                    return StreamErrorAction.Cancel;
                }

#pragma warning disable CA1862 // Support target frameworks without the StringComparison overload.
                if (BadArgument.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase)
                    && errorResponse.Body?.Error?.Message?.ToLower().Contains(TeamsStreamNotAllowed) == true)
                {
                    Context.Adapter?.Logger?.LogWarning(
                        "Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");
                    System.Diagnostics.Trace.WriteLine(
                        "Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");
                    return StreamErrorAction.FallbackToNonStreaming;
                }
#pragma warning restore CA1862

                var errorMessage = errorResponse.Body?.Error?.Message ?? "None";
                Context.Adapter?.Logger?.LogWarning(
                    "Exception during StreamingResponse: {ExceptionMessage} - {ErrorMessage}",
                    exception.Message,
                    errorMessage);
                System.Diagnostics.Trace.WriteLine(
                    $"Exception during StreamingResponse: {exception.Message} - {errorMessage}");

                return StreamErrorAction.Cancel;
            }

            return await base.HandleSendErrorAsync(exception, cancellationToken).ConfigureAwait(false);
        }

        protected override IActivity CreateFinalMessage()
        {
            var activity = base.CreateFinalMessage();

            if (_streamTimedOut && !string.IsNullOrEmpty(StreamId))
            {
                activity.Id = StreamId;
            }

            if (FeedbackLoopEnabled)
            {
                activity.ChannelData = ObjectPath.Merge(activity.ChannelData, new
                {
                    feedbackLoop = new
                    {
                        type = FeedbackLoopType ?? "default"
                    }
                });
            }

            return activity;
        }

        protected override Task<ResourceResponse> SendNonStreamingFinalMessageAsync(
            IActivity activity,
            CancellationToken cancellationToken = default)
        {
            return _streamTimedOut
                ? UpdateActivityAsync(activity, cancellationToken)
                : base.SendNonStreamingFinalMessageAsync(activity, cancellationToken);
        }

        protected override void OnReset()
        {
            base.OnReset();
            _streamTimedOut = false;
            SetDefaults(Context);
        }

        private Activity CreateStreamTimedOutMessage()
        {
            return new Activity
            {
                Type = ActivityTypes.Message,
                Id = StreamId,
                Text = !string.IsNullOrEmpty(Message)
                    ? $"{Message} {Environment.NewLine}{Environment.NewLine} {StreamingTakingTooLongMessage} {Environment.NewLine}"
                    : StreamingTakingTooLongMessage
            };
        }

        private async Task<ResourceResponse> UpdateActivityAsync(
            IActivity activity,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await Context.UpdateActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ErrorResponseException errorResponse)
                {
                    Context.Adapter?.Logger?.LogWarning(
                        "Exception during StreamingResponse UpdateActivity: {ExceptionMessage} - {ErrorMessage}",
                        ex.Message,
                        errorResponse.Body?.Error?.Message ?? "None");
                }
                else
                {
                    Context.Adapter?.Logger?.LogWarning(
                        "Exception during StreamingResponse UpdateActivity: {ExceptionMessage}",
                        ex.Message);
                }

                System.Diagnostics.Trace.WriteLine(
                    $"Exception during StreamingResponse UpdateActivity: {ex.Message}");
                return null;
            }
        }
    }
}
