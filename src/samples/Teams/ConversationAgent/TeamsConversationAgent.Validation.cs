// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConversationAgent;

public partial class TeamsConversationAgent
{
    [MessageRoute("validate")]
    public static Task ValidationHelpAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken) =>
        SendValidationReportAsync(context,
            "Teams validation commands:\n" +
            "validateattachments - inspect this turn's attachment shapes and downloaded files\n" +
            "validateconversion - check conversation/sender/recipient conversion\n" +
            "validatemarkdown - render extended markdown\n" +
            "validatelabel - send the configured sensitivity label\n" +
            "validatestream - stream text with a final card and optional label\n" +
            "validatefinaltimeout - opt-in simulated final-send error and real recovery update\n" +
            "validatemeeting - inspect typed meeting metadata\n" +
            "Sending attachments also produces a report automatically. Use small, non-sensitive test files.",
            cancellationToken);

    [MessageRoute("validateattachments")]
    public static Task ReportAttachmentsAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken)
    {
        var attachments = context.Activity.Attachments;
        var files = state.Temp.InputFiles;
        var report = new StringBuilder("Attachment validation\n")
            .AppendLine($"Incoming attachments: {attachments?.Count ?? 0}")
            .AppendLine($"Downloaded files: {files.Count}")
            .AppendLine($"Conversation scope: {Display(context.Activity.Conversation?.ConversationType)}")
            .AppendLine($"Recipient role: {Display(context.Activity.Recipient?.Role)}");
        foreach (var attachment in (attachments ?? []).Take(10))
        {
            report.AppendLine($"Incoming: {Display(attachment.Name)}; {Display(attachment.ContentType)}; " +
                $"ContentUrl present: {!string.IsNullOrEmpty(attachment.ContentUrl)}; " +
                $"downloadUrl declared: {HasDownloadUrl(attachment)}");
        }
        foreach (var file in files.Take(10))
        {
            var bytes = file.Content.ToMemory();
            report.AppendLine($"Downloaded: {Display(file.Filename)}; {Display(file.ContentType)}; {bytes.Length} bytes; " +
                $"SHA256={Convert.ToHexString(SHA256.HashData(bytes.Span))}");
        }
        if ((attachments?.Count ?? 0) > 10 || files.Count > 10)
        {
            report.AppendLine("Only the first 10 entries of each collection are shown.");
        }
        if (files.Count == 0)
        {
            report.AppendLine("No files downloaded. Check the incoming shape, identity/permissions and downloader warning logs. " +
                "A plain sharing link is not guaranteed to arrive as an attachment.");
        }
        return SendValidationReportAsync(context, report.ToString(), cancellationToken);
    }

    [MessageRoute("validateconversion")]
    public static Task ValidateConversionAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken)
    {
        var converted = context.Activity.ToTeamsActivity();
        return SendValidationReportAsync(context,
            $"Conversation ID: {Preserved(context.Activity.Conversation?.Id, converted.Conversation?.Id)}\n" +
            $"Sender ID: {Preserved(context.Activity.From?.Id, converted.From?.Id)}\n" +
            $"Recipient ID: {Preserved(context.Activity.Recipient?.Id, converted.Recipient?.Id)}\n" +
            "Identifiers are compared, not displayed.", cancellationToken);
    }

    [MessageRoute("validatemarkdown")]
    public static Task ValidateMarkdownAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken) =>
        SendValidationActivityAsync(context, new Activity
        {
            Type = ActivityTypes.Message,
            TextFormat = "extendedmarkdown",
            Text = "## Extended markdown validation\n\n- [x] Checklist item\n\n| Check | Result |\n| --- | --- |\n| Formatting | **Visible** |"
        }, cancellationToken);

    [MessageRoute("validatelabel")]
    public Task ValidateLabelAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken)
    {
        var label = GetValidationLabel();
        return label == null
            ? SendValidationReportAsync(context, "Configure TeamsValidation:SensitivityLabelId with an actual Purview label GUID first.", cancellationToken)
            : SendValidationActivityAsync(context, new Activity
            {
                Type = ActivityTypes.Message,
                Text = "Sensitivity label validation. This message is labeled without claiming AI-generated content.",
                Entities = [new AIEntity { UsageInfo = label }]
            }, cancellationToken);
    }

    [MessageRoute("validatemeeting")]
    public static Task ValidateMeetingAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken)
    {
        var meeting = context.Activity.TeamsGetMeetingInfo();
        return SendValidationReportAsync(context,
            meeting == null ? "No meeting metadata was supplied on this activity." :
                $"Typed meeting metadata resolved. Meeting ID present: {!string.IsNullOrEmpty(meeting.Id)}",
            cancellationToken);
    }

    [MessageRoute("validatestream")]
    public Task ValidateStreamAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken) =>
        RunValidationStreamAsync(context, false, cancellationToken);

    [MessageRoute("validatefinaltimeout")]
    public Task ValidateFinalTimeoutAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken) =>
        RunValidationStreamAsync(context, true, cancellationToken);

    private async Task RunValidationStreamAsync(ITurnContext context, bool simulateTimeout, CancellationToken cancellationToken)
    {
        if (new TeamsTurnContext(context).Activity.IsRecipientTargeted())
        {
            await SendValidationReportAsync(context, "Run streaming validation in a normal bot chat, not a private targeted prompt.", cancellationToken);
            return;
        }
        if (simulateTimeout && !_validationConfiguration.GetValue<bool>("TeamsValidation:EnableFinalSendFault"))
        {
            await SendValidationReportAsync(context,
                "Final-send fault injection is disabled. Enable TeamsValidation:EnableFinalSendFault only in a test deployment.",
                cancellationToken);
            return;
        }
        if (!context.StreamingResponse.IsStreamingChannel)
        {
            await SendValidationReportAsync(context, "Run this check in Teams or another streaming-capable channel.", cancellationToken);
            return;
        }

        const string finalText = "Streaming validation completed.";
        var label = GetValidationLabel();
        var injected = false;
        var updateSucceeded = false;
        var payloadPreserved = false;
        if (simulateTimeout)
        {
            context.OnSendActivities(async (_, activities, next) =>
            {
                var final = activities.Any(activity => activity.GetStreamingEntity()?.StreamType == StreamTypes.Final);
                var responses = await next();
                if (final && !injected)
                {
                    // Fail after acceptance so Teams has a finalized activity that can be updated.
                    injected = true;
                    throw new ErrorResponseException("Sample-injected final-send timeout.")
                    {
                        Body = new ErrorResponse(new Error
                        {
                            Code = "ContentStreamNotAllowed",
                            Message = "Content stream finished due to exceeded streaming time."
                        })
                    };
                }
                return responses;
            });
            context.OnUpdateActivity(async (_, activity, next) =>
            {
                var response = await next();
                updateSucceeded = response != null;
                payloadPreserved = activity.Text == finalText && activity.Attachments?.Count == 1 &&
                    activity.GetStreamingEntity() == null &&
                    (label == null || activity.Entities.OfType<AIEntity>().Any(entity => entity.UsageInfo?.AtId == label.AtId));
                return response ?? throw new InvalidOperationException("The update returned no resource response.");
            });
        }

        await context.StreamingResponse.QueueInformativeUpdateAsync("Starting streaming validation...", cancellationToken);
        context.StreamingResponse.QueueTextChunk(finalText);
        context.StreamingResponse.AddAttachment(new Attachment
        {
            ContentType = "application/vnd.microsoft.card.hero",
            Content = new HeroCard { Title = "Final validation card", Text = "This card must survive final-send recovery." }
        });
        context.StreamingResponse.SensitivityLabel = label;
        var result = await context.StreamingResponse.EndStreamAsync(cancellationToken);
        _logger.LogInformation("Streaming validation result: {Result}; injected={Injected}; updated={Updated}; payload={PayloadPreserved}",
            result, injected, updateSucceeded, payloadPreserved);
        if (simulateTimeout)
        {
            var passed = injected && updateSucceeded && payloadPreserved && result == StreamingResponseResult.Success;
            await SendValidationReportAsync(context,
                $"Recovery validation: {(passed ? "PASS" : "FAIL")}\n" +
                $"SDK result: {result}; final-send error injected: {injected}; update succeeded: {updateSucceeded}; payload preserved: {payloadPreserved}.\n" +
                "The error was simulated after the final send was accepted. This does not test the actual ten-minute service limit.",
                cancellationToken);
        }
    }

    private SensitivityUsageInfo? GetValidationLabel() =>
        Guid.TryParse(_validationConfiguration["TeamsValidation:SensitivityLabelId"], out var id) && id != Guid.Empty
            ? new SensitivityUsageInfo { AtId = id.ToString(), Name = "Validation label", Position = 1 }
            : null;

    private static Task SendValidationReportAsync(ITurnContext context, string text, CancellationToken cancellationToken) =>
        SendValidationActivityAsync(context, new Activity { Type = ActivityTypes.Message, Text = text, TextFormat = TextFormatTypes.Plain }, cancellationToken);

    private static Task SendValidationActivityAsync(ITurnContext context, IActivity activity, CancellationToken cancellationToken)
    {
        var teamsContext = context as ITeamsTurnContext ?? new TeamsTurnContext(context);
        return teamsContext.Activity.IsRecipientTargeted()
            ? teamsContext.SendTargetedActivityAsync(activity, teamsContext.Activity.From, cancellationToken)
            : context.SendActivityAsync(activity, cancellationToken);
    }

    private static string Preserved(string? original, string? converted) =>
        original == null ? "not present on input" : original == converted ? "PASS" : "FAIL";

    private static string Display(string? value)
    {
        var text = (value ?? "(none)").Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= 160 ? text : text.Substring(0, 160);
    }

    private static bool HasDownloadUrl(Attachment attachment)
    {
        if (attachment.Content == null || attachment.ContentType == "text/html")
        {
            return false;
        }
        try
        {
            var content = JsonSerializer.SerializeToElement(attachment.Content);
            if (content.ValueKind == JsonValueKind.String)
            {
                using var document = JsonDocument.Parse(content.GetString()!);
                return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("downloadUrl", out _);
            }
            return content.ValueKind == JsonValueKind.Object && content.TryGetProperty("downloadUrl", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
