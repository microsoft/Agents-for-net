using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    internal class ScopeOnTurn : TelemetryScope
    {

        private readonly ITurnContext _turnContext;
        private bool? _routeAuthorized = null;
        private bool? _routeMatched = null;

        public ScopeOnTurn(ITurnContext turnContext) : base(Constants.ScopeOnTurn)
        {
            _turnContext = turnContext;
        }
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            TagList tags = new();
            tags.Add(TagNames.ActivityType, _turnContext.Activity.Type);
            tags.Add(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());
            tags.Add(TagNames.ConversationId, _turnContext.Activity.Conversation?.Id);
            tags.Add(TagNames.ActivityId, _turnContext.Activity.Id);

            telemetryActivity.SetTag(TagNames.ActivityType, _turnContext.Activity.Type);
            telemetryActivity.SetTag(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());
            telemetryActivity.SetTag(TagNames.ConversationId, _turnContext.Activity.Conversation?.Id);
            telemetryActivity.SetTag(TagNames.ActivityId, _turnContext.Activity.Id);
            telemetryActivity.SetTag(TagNames.RouteAuthorized, _routeAuthorized);
            telemetryActivity.SetTag(TagNames.RouteMatched, _routeMatched);

            TagList metricTags = new();
            metricTags.Add(TagNames.ActivityType, _turnContext.Activity.Type);
            metricTags.Add(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());

            if (error == null)
            {
                Metrics.TurnCount.Add(1, metricTags);
                Metrics.TurnDuration.Record(duration, metricTags);
            }
            else
            {
                Metrics.TurnErrorCount.Add(1, metricTags);
            }
        }

        public void Share(bool routeAuthorized, bool routeMatched)
        {
            _routeAuthorized = routeAuthorized;
            _routeMatched = routeMatched;
        }
    }
}
