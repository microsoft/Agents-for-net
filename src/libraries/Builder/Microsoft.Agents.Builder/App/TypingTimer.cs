// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Encapsulates the logic for sending "typing" activity to the user.
    /// </summary>
    internal class TypingTimer : IDisposable
    {
        private Timer? _timer;
        /// <summary>
        /// The interval in milliseconds to send "typing" activity.
        /// </summary>
        private readonly int _interval;

        // For synchronizing SendActivity and Typing to prevent race
        private static AutoResetEvent _send;  

        /// <summary>
        /// Initial delay before first typing is sent.
        /// </summary>
        private readonly int _initialDelay;

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        private bool _disposedValue = false;

        /// <summary>
        /// Constructs a new instance of the <see cref="TypingTimer"/> class.
        /// </summary>
        /// <param name="interval">The interval in milliseconds to send "typing" activity.</param>
        /// <param name="initialDelay">Initial delay</param>
        public TypingTimer(int interval = 2000, int initialDelay = 500)
        {
            _interval = interval;
            _initialDelay = initialDelay;
        }

        /// <summary>
        /// Manually start a timer to periodically send "typing" activity.
        /// </summary>
        /// <remarks>
        /// The timer will automatically end once an outgoing activity has been sent. If the timer is already running or 
        /// the current activity is not a "message" the call is ignored.
        /// </remarks>
        /// <param name="turnContext">The context for the current turn with the user.</param>
        /// <returns>True if the timer was started, otherwise False.</returns>
        public bool Start(ITurnContext turnContext)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            if (turnContext.Activity.Type != ActivityTypes.Message || IsRunning())
            {
                return false;
            }

            // Stop timer when message activities are sent
            turnContext.OnSendActivities(StopTimerWhenSendMessageActivityHandlerAsync);

            _send = new AutoResetEvent(false);

            // Start periodically send "typing" activity
            _timer = new Timer(SendTypingActivity, turnContext, Timeout.Infinite, Timeout.Infinite);

            // Fire first time
            _timer.Change(_initialDelay, Timeout.Infinite);

            return true;
        }

        /// <summary>
        /// Stop the timer that periodically sends "typing" activity.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_timer != null)
                    {
                        _timer.Dispose();
                        _timer = null;
                    }
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Whether there is a timer currently running.
        /// </summary>
        /// <returns>True if there's a timer currently running, otherwise False.</returns>
        public bool IsRunning()
        {
            return _timer != null;
        }

        private async void SendTypingActivity(object state)
        {
            ITurnContext turnContext = state as ITurnContext ?? throw new ArgumentException("Unexpected failure of casting object TurnContext");

            try
            {
                if (_timer != null)
                {
                    await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing, RelatesTo = turnContext.Activity.RelatesTo, Text = "TYPING" }, CancellationToken.None).ConfigureAwait(false);
                    if (IsRunning())
                    {
                        _timer?.Change(_interval, Timeout.Infinite);
                        _send.Set();
                    }
                }
            }
            catch (Exception e) when (e is ObjectDisposedException || e is TaskCanceledException || e is NullReferenceException)
            {
                // We're in the middle of sending an activity on a background thread when the turn ends and
                // the turn context object is disposed of or the request is cancelled. We can just eat the
                // error but lets make sure our states cleaned up a bit.
                Dispose();
            }
        }

        private Task<ResourceResponse[]> StopTimerWhenSendMessageActivityHandlerAsync(ITurnContext turnContext, List<IActivity> activities, Func<Task<ResourceResponse[]>> next)
        {
            if (_timer != null)
            {
                foreach (IActivity activity in activities)
                {
                    if (activity.Type == ActivityTypes.Message)
                    {
                        // This will block ITurnContext.SendActivity until the typing timer is done.
                        _send.WaitOne();

                        // Stop timer
                        Dispose();

                        break;
                    }
                }
            }

            return next();
        }
    }
}
