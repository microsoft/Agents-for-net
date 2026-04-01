using Microsoft.Agents.Core.Telemetry;
using System.Collections;

namespace Microsoft.Agents.Builder.Telemetry.TurnContext
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the sending of activities from within
    /// a turn context.
    /// </summary>
    /// <remarks>
    /// Tags the <see cref="System.Diagnostics.Activity"/> with the conversation identifier
    /// from the current turn's activity.
    /// </remarks>
    internal class ScopeSendActivities : TelemetryScope
    {
        private readonly ITurnContext _turnContext;
        public ScopeSendActivities(ITurnContext turnContext) : base(Constants.ScopeSendActivities)
        {
            _turnContext = turnContext;
        }
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, System.Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ConversationId, _turnContext.Activity.Conversation?.Id);
        }
    }
}