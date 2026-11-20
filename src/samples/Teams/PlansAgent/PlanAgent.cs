// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using CitationIcons = Microsoft.Teams.Apps.Schema.Entities.CitationIcons;
using PlanStep = Microsoft.Teams.Apps.Schema.Entities.PlanStep;
using PlanStepStatuses = Microsoft.Teams.Apps.Schema.Entities.PlanStepStatuses;
using StepSource = Microsoft.Teams.Apps.Schema.Entities.StepSource;

#pragma warning disable ExperimentalTeamsPlan // This sample explicitly opts into experimental Plan APIs.

namespace PlansAgent;

[TeamsExtension]
public partial class PlanAgent(AgentApplicationOptions options, ILogger<PlanAgent> logger) : AgentApplication(options)
{
    [TeamsMessageRoute]
    private async Task OnMessageAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (turnContext.Activity.IsRecipientTargeted() || turnContext.Activity.IsTargetedActivity())
        {
            await turnContext.SendTargetedActivityAsync(
                "Experimental Plans do not support targeted prompts. Ask in the bot's normal personal chat instead.",
                turnContext.Activity.From, cancellationToken);
            return;
        }

        var command = turnContext.Activity.Text?.Trim().ToLowerInvariant();
        if (command is not ("complete" or "complete-stream" or "stop" or "error"))
        {
            await turnContext.SendActivityAsync(
                "Try: complete, complete-stream, stop, or error. These commands demonstrate Plan progress with fixed example data; no AI resource is required.",
                cancellationToken: cancellationToken);
            return;
        }

        var plan = turnContext.CreatePlan();
        var prepare = new PlanStep
        {
            Id = "prepare",
            Version = 1,
            Title = "Prepare example",
            Status = PlanStepStatuses.Active
        };
        var response = new PlanStep
        {
            Id = "response",
            Version = 1,
            Title = "Deliver example",
            Status = PlanStepStatuses.Proposed
        };
        await plan.StartAsync([prepare, response], cancellationToken);
        await PauseForDisplayAsync(cancellationToken);

        if (command == "stop")
        {
            await plan.StopAsync(cancellationToken);
            return;
        }
        if (command == "error")
        {
            prepare.Status = PlanStepStatuses.Error;
            prepare.Description = "This command demonstrates a failed step.";
            prepare.Version++;
            await plan.UpdateAsync([prepare], cancellationToken);
            await plan.ErrorAsync(cancellationToken);
            return;
        }

        prepare.Status = PlanStepStatuses.Blocked;
        prepare.Description = "This revision demonstrates a temporarily blocked step.";
        prepare.Version++;
        await plan.UpdateAsync([prepare], cancellationToken);
        await PauseForDisplayAsync(cancellationToken);

        prepare.Status = PlanStepStatuses.Completed;
        prepare.Description = "Fixed local demonstration data is ready.";
        prepare.Sources =
        [
            new StepSource
            {
                Title = "Agents SDK documentation",
                Url = new Uri("https://learn.microsoft.com/microsoft-365/agents-sdk/"),
                Icon = CitationIcons.Text
            }
        ];
        prepare.Version++;
        response.Status = PlanStepStatuses.Active;
        response.Version++;
        await plan.UpdateAsync([prepare, response], cancellationToken);

        string[] chunks =
        [
            "This is a fixed demonstration response. ",
            "The application reported each step explicitly, ",
            "and this message completes the Plan."
        ];
        if (command == "complete-stream")
        {
            var stream = turnContext.StreamingResponse;
            await stream.QueueInformativeUpdateAsync("Streaming the demonstration response.", cancellationToken);
            foreach (var chunk in chunks)
            {
                stream.QueueTextChunk(chunk);
                await PauseForDisplayAsync(cancellationToken);
            }
        }
        else
        {
            await PauseForDisplayAsync(cancellationToken);
        }

        response.Status = PlanStepStatuses.Completed;
        response.Version++;
        await plan.UpdateAsync([response], cancellationToken);
        if (command == "complete-stream")
        {
            var result = await plan.CompleteStreamingAsync(cancellationToken);
            if (result != StreamingResponseResult.Success)
            {
                logger.LogWarning("Plan stream ended with {Result}; completion will not be retried automatically.", result);
            }
        }
        else
        {
            await plan.CompleteAsync(string.Concat(chunks), cancellationToken);
        }
    }

    // These pauses only keep the scripted progress visible in Teams.
    private static Task PauseForDisplayAsync(CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
}

#pragma warning restore ExperimentalTeamsPlan
