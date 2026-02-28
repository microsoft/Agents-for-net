// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{

	public sealed class TimedActivity : IDisposable
	{
		private readonly Activity? Activity;
		private readonly Stopwatch Stopwatch;
		private readonly Action<Activity?, long>? SuccessCallback;
		private readonly Action<Activity?, long>? FailureCallback;
		private bool Disposed;
		private bool HasError;

		internal TimedActivity(
			Activity? activity,
			Action<Activity?, long>? successCallback,
			Action<Activity?, long>? failureCallback
		)
		{
			Activity = activity;
			Stopwatch = Stopwatch.StartNew();
			SuccessCallback = successCallback;
			FailureCallback = failureCallback;
		}

		public void SetError(Exception ex)
		{
			if (Activity == null)
				return;

			HasError = true;
			Activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));
        }

		public void Dispose()
		{
			if (Disposed)
				return;

			Stopwatch.Stop();
			var duration = Stopwatch.ElapsedMilliseconds;

			if (HasError && FailureCallback != null)
				FailureCallback(Activity, duration);
			else if (!HasError && SuccessCallback != null)
				SuccessCallback(Activity, duration);

			Activity?.Dispose();

			Disposed = true;
		}
	}

	public static class AgentTelemetry
	{
		
		public static readonly string SourceName = "Microsoft.Agents.Builder";
		public static readonly string SourceVersion = "1.0.0";
		public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);

		private static readonly Meter Meter = new(SourceName, SourceVersion);
		private static readonly Counter<long> TurnTotal = Meter.CreateCounter<long>(
			"agent.turns.total", "turn"
		);
		private static readonly Counter<long> TurnErrors = Meter.CreateCounter<long>(
			"agent.turns.errors", "turn"
		);
        private static readonly Histogram<long> TurnDuration = Meter.CreateHistogram<long>(
			"agent.turn.duration", "ms");

        private static Dictionary<string, string> ExtractAttributesFromContext(ITurnContext turnContext)
		{
			Dictionary< string, string> attributes = new Dictionary<string, string>();
			attributes["activity.type"] = turnContext.Activity.Type;
			attributes["agent.is_agentic"] = turnContext.IsAgenticRequest().ToString();
			if (turnContext.Activity.From != null)
			{
				attributes["from.id"] = turnContext.Activity.From.Id;
            }
			if (turnContext.Activity.Recipient != null)
			{
				attributes["recipient.id"] = turnContext.Activity.Recipient.Id;
            }
			if (turnContext.Activity.Conversation != null)
			{
				attributes["conversation.id"] = turnContext.Activity.Conversation.Id;
            }
			attributes["channel_id"] = turnContext.Activity.ChannelId;
			attributes["message.text.length"] = turnContext.Activity.Text?.Length.ToString() ?? "0";
            return attributes;
        }

		public static Activity? StartActivity(string name, ITurnContext? turnContext)
		{
			var activity = ActivitySource.StartActivity(name);
			if (turnContext != null)
			{
                var attributes = ExtractAttributesFromContext(turnContext);
                foreach (var kvp in attributes)
				{
					activity?.SetTag(kvp.Key, kvp.Value);
                }
            }
			return activity;
        }

        private static TimedActivity StartTimedActivity(
			string operationName,
			ITurnContext? turnContext,
			Action<Activity, long>? successCallback = null,
			Action<Activity, long>? failureCallback = null
			)
		{
			var activity = StartActivity(operationName, turnContext);
			return new TimedActivity(
				activity,
				successCallback,
				failureCallback
			);
		}

		public static TimedActivity StartAgentTurnOperation(ITurnContext turnContext)
		{
			return StartTimedActivity(
				"agent turn",
				turnContext,
				successCallback: (activity, duration) =>
				{
					TurnTotal.Add(1);
					TurnDuration.Record(duration);
				},
				failureCallback: (activity, duration) =>
				{
					TurnTotal.Add(1);
					TurnErrors.Add(1);
					TurnDuration.Record(duration);
				}
			);
        }

		public static TimedActivity StartAdapterProcessOperation()
		{
			return StartTimedActivity(
				"adapter process",
				null,
				successCallback: (activity, duration) =>
				{
					// do stuff
				},
				failureCallback: (activity, duration) =>
				{
					// do stuff
				}
			);
		}

		public static TimedActivity StartStorageOperation(string operationName)
		{
			return StartTimedActivity(
				$"storage {operationName}",
				null,
				successCallback: (activity, duration) =>
				{
					// do stuff
				},
				failureCallback: (activity, duration) =>
				{
					// do stuff
				}
			);
		}
    }
}