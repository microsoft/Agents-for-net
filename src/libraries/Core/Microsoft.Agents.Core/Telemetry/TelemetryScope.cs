using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    public class TelemetryScope : IDisposable
    {
        public readonly Activity? _telemetryActivity;
        private Exception? _error = null;
        private bool _disposed = false;

        public TelemetryScope(string activityName, ActivityKind activityKind = ActivityKind.Internal)
        {
            _telemetryActivity = AgentsTelemetry.ActivitySource.StartActivity(
                activityName,
                activityKind
            );
        }

        public void SetError(Exception ex)
        {
            if (_telemetryActivity == null)
            {
                return;
            }

            _telemetryActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetryActivity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));
            _error = ex;
        }

        protected virtual void Callback(Activity activity, double duration, Exception? exception)
        {
            // Override this method in derived classes to perform custom actions when the activity is disposed.
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here if needed
                    if (_telemetryActivity != null)
                    {
                        Callback(_telemetryActivity, _telemetryActivity.Duration.TotalMilliseconds, _error);
                        _telemetryActivity.Dispose();
                    }
                }
                // Dispose unmanaged resources here if needed
                _disposed = true;
            }
        }
    }
}
