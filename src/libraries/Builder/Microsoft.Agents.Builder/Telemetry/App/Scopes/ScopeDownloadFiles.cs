using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    internal class ScopeDownloadFiles : TelemetryScope
    {
        private readonly ITurnContext _turnContext;

        public ScopeDownloadFiles(ITurnContext turnContext) : base(Constants.ScopeDownloadFiles)
        {
            _turnContext = turnContext;
        }

        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.AttachmentCount, _turnContext.Activity.Attachments?.Count ?? 0);
        }
    }
}
