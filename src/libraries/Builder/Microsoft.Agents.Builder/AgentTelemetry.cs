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

	public static class AgentTelemetry
	{
		
		public static readonly string SourceName = "Microsoft.Agents.Builder";
		public static readonly ActivitySource ActivitySource = new(SourceName);

		private static readonly Meter Meter = new("Microsoft.Agents.Builder", "1.0.0");
		private static readonly Counter<long> TurnTotal = Meter.CreateCounter<long>(
			"agent.turns.total", "turn"
		);
		private static readonly Counter<long> TurnErrors = Meter.CreateCounter<long>(
			"agent.turns.errors", "turn"
		);
        private static readonly Histogram<long> TurnDuration = Meter.CreateHistogram<long>(
			"agent.turn.duration", "ms");

        private static Dictionary<string, string> ExtractAttributesFromContext(TurnContext context)
		{
			Dictionary< string, string> attributes = new Dictionary<string, string>();
			attributes["activity.type"] = context.Activity.Type;
			attributes["agent.is_agentic"] = context.IsAgenticRequest().ToString();
			if (context.Activity.From != null)
			{
				attributes["from.id"] = context.Activity.From.Id;
            }
			if (context.Activity.Recipient != null)
			{
				attributes["recipient.id"] = context.Activity.Recipient.Id;
            }
			if (context.Activity.Conversation != null)
			{
				attributes["conversation.id"] = context.Activity.Conversation.Id;
            }
			attributes["channel_id"] = context.Activity.ChannelId;
			attributes["message.text.length"] = context.Activity.Text?.Length.ToString() ?? "0";
            return attributes;
        }

		public static Activity StartActivity(string name, TurnContext? context)
		{
			var activity = ActivitySource.StartActivity(name, ActivityKind.Server);
			if (context != null)
			{
                var attributes = ExtractAttributesFromContext(context);
                foreach (var kvp in attributes)
				{
					activity?.SetTag(kvp.Key, kvp.Value);
                }
            }
			return activity;
        }

		private static async Task TimedActivityAsync(
			string operationName,
			TurnContext? context,
			Func<Task> func,
			Action<Activity, long>? successCallback = null,
			Action<Activity, long>? failureCallback = null
			)
		{
			using var activity = StartActivity(operationName, context);
			bool success = true;

			var stopwatch = Stopwatch.StartNew();

			try
			{
				await func();
			}
			catch (Exception ex)
			{
				success = false;
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
				{
					["exception.type"] = ex.GetType().FullName,
					["exception.message"] = ex.Message,
					["exception.stacktrace"] = ex.StackTrace
				}));
				throw;
            }
			finally
			{
				stopwatch.Stop();
				var duration = stopwatch.ElapsedMilliseconds;

				if (success)
				{
					activity?.SetStatus(ActivityStatusCode.Ok);
					if (successCallback != null && activity != null)
					{
						successCallback(activity, duration);
					}
                }
				else
				{
					if (failureCallback != null && activity != null)
					{
						failureCallback(activity, duration);
                    }

					activity?.SetStatus(ActivityStatusCode.Error);
                }
            }
		}

		public static async Task InvokeAgentTurnOperation(TurnContext context, Func<Task> func)
		{
			await TimedActivityAsync(
				"agent turn",
				context,
				func,
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

		public static async Task InvokeAdapterProcessOperation(Func<Task> func)
		{
			await TimedActivityAsync(
				"adapter process",
				null,
				func,
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

		public static async Task InvokeStorageOperation(string operationName, Func<Task> func)
		{
			await TimedActivityAsync(
				$"storage {operationName}",
				null,
				func,
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

		public static Activity? StartActivity(string operationName)
		{
			return ActivitySource.StartActivity(operationName);
		}
    }
}