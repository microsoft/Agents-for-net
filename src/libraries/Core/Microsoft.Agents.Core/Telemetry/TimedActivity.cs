using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    public sealed class TimedActivity : IDisposable
    {
        private readonly Activity? Activity;
        private readonly Stopwatch Stopwatch;
        private readonly Action<Activity?, long, Exception?>? Callback;
        private bool Disposed;
        private Exception Error;

        public TimedActivity(
            Activity? activity,
            Action<Activity?, long, Exception?>? callback
        )
        {
            Activity = activity;
            Stopwatch = Stopwatch.StartNew();
            Callback = callback;
        }

        public void SetError(Exception ex)
        {
            if (Activity == null)
                return;

            Activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));
            Error = ex;
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Stopwatch.Stop();

            Callback(
                Activity,
                Stopwatch.ElapsedMilliseconds,
                Error
            );

            Activity?.Dispose();

            Disposed = true;
        }
    }
}
