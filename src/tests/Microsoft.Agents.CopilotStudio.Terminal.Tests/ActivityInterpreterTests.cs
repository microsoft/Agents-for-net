#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Agents.Core.Models;

public sealed class ActivityInterpreterTests
{
    [Fact]
    public void Process_ThoughtEntityVariants_ShowThoughtEntriesCaseInsensitively()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Text = "Answer",
            Entities =
            [
                new Entity("THOUGHT")
                {
                    Properties = { ["content"] = JsonSerializer.SerializeToElement("Checking mail") }
                },
                new Entity("thoughts")
                {
                    Properties = { ["description"] = JsonSerializer.SerializeToElement("Calling tools") }
                },
                new Entity("clientInfo")
                {
                    Properties = { ["platform"] = JsonSerializer.SerializeToElement("Web") }
                }
            ]
        };

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Agent && change.Entry.Text == "Answer");
        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Thought && change.Entry.Text == "Checking mail");
        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Thought && change.Entry.Text == "Calling tools");
        Assert.DoesNotContain(changes, change => change.Entry?.Text.Contains("clientInfo", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Process_ThoughtWithoutStringFields_UsesIndentedEntityJson()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Entities =
            [
                new Entity("thought")
                {
                    Properties = { ["value"] = JsonSerializer.SerializeToElement(new { status = "running" }) }
                }
            ]
        };

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        ChatEntry entry = Assert.Single(changes, change => change.Entry?.Kind == ChatEntryKind.Thought).Entry!;
        Assert.Contains(Environment.NewLine, entry.Text);
        Assert.Contains("\"status\": \"running\"", entry.Text);
    }

    [Fact]
    public void Process_ActionFreeAdaptiveCard_AddsAttachmentWithoutDiagnostic()
    {
        Activity activity = CreateMessageWithCard("""{"type":"AdaptiveCard","body":[{"type":"FactSet","facts":[]}]}""");

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        ChatEntry attachment = Assert.Single(changes, change => change.Entry?.Kind == ChatEntryKind.Attachment).Entry!;
        Assert.Empty(attachment.Links);
        Assert.DoesNotContain(changes, change => change.Entry?.Kind == ChatEntryKind.Diagnostic);
    }

    [Fact]
    public void Process_MalformedAdaptiveCard_PreservesAttachmentAndAddsDiagnostic()
    {
        Activity activity = CreateMessageWithCard("{bad");

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Attachment);
        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Diagnostic);
    }

    [Fact]
    public void Process_OutboundMessage_RendersUserEntry()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Text = "Hi there"
        };

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Outbound);

        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.User && change.Entry.Text == "Hi there");
    }

    [Fact]
    public void Process_OrdinaryTyping_AddsStatusEntry()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Typing,
            Text = "Working..."
        };

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Status && change.Entry.Text == "Working...");
    }

    [Fact]
    public void Process_Event_AddsEventEntry()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Event,
            Name = "handoff.completed"
        };

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Event && change.Entry.Text == "handoff.completed");
    }

    [Fact]
    public void Process_SuggestedActions_UsesTextThenTitleAndStringifiesValue()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Text = "Choose",
            SuggestedActions = new SuggestedActions(actions:
            [
                new CardAction { Text = "Use text", Title = "Ignored", Value = 42 },
                new CardAction { Title = "Use title", Value = "second" }
            ])
        };

        IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

        ChatEntry entry = Assert.Single(changes, change => change.Entry?.Kind == ChatEntryKind.Agent).Entry!;
        Assert.Equal(
            [
                new ChatAction("Use text", "42"),
                new ChatAction("Use title", "second")
            ],
            entry.SuggestedActions);
    }

    [Fact]
    public void Process_TranscriptStream_UsesStartActivityIdAndFinalWithoutSequence()
    {
        ActivityInterpreter interpreter = new();
        Activity start = StreamActivity(ActivityTypes.Typing, "start-1", "Just a moment please..", null, StreamTypes.Informative, 1);
        Activity update = StreamActivity(ActivityTypes.Typing, "update-1", "Loading tools...", "start-1", StreamTypes.Informative, 2);
        Activity chunk = StreamActivity(ActivityTypes.Typing, "update-2", "Hello! How can I assist?", "start-1", StreamTypes.Streaming, 3);
        Activity final = StreamActivity(ActivityTypes.Message, "final-1", "Hello! How can I assist?", "start-1", StreamTypes.Final, null);

        AssertStatus(interpreter.Process(start, ActivityDirection.Inbound), "Just a moment please..", "stream:start-1:status");
        AssertStatus(interpreter.Process(update, ActivityDirection.Inbound), "Loading tools...", "stream:start-1:status");
        AssertAgentUpsert(interpreter.Process(chunk, ActivityDirection.Inbound), "Hello! How can I assist?", "stream:start-1:response", transient: true);

        IReadOnlyList<ChatChange> finalChanges = interpreter.Process(final, ActivityDirection.Inbound);

        AssertAgentUpsert(finalChanges, "Hello! How can I assist?", "stream:start-1:response", transient: false);
        Assert.Contains(finalChanges, change => change.Kind == ChatChangeKind.Remove && change.Key == "stream:start-1:status");
    }

    [Fact]
    public void Process_StartWithoutSequence_UsesActivityIdWhenNoStreamExists()
    {
        ActivityInterpreter interpreter = new();

        IReadOnlyList<ChatChange> changes = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "start-1", "Starting", null, StreamTypes.Informative, null),
            ActivityDirection.Inbound);

        AssertStatus(changes, "Starting", "stream:start-1:status");
    }

    [Fact]
    public void Process_UpdateWithoutStreamId_CorrelatesToOnlyOpenStream()
    {
        ActivityInterpreter interpreter = new();
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "start-1", "Starting", null, StreamTypes.Informative, 1),
            ActivityDirection.Inbound);

        IReadOnlyList<ChatChange> changes = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "update-1", "Partial answer", null, StreamTypes.Streaming, 2),
            ActivityDirection.Inbound);

        AssertAgentUpsert(changes, "Partial answer", "stream:start-1:response", transient: true);
        Assert.DoesNotContain(changes, change => change.Key == "stream:update-1:response");
    }

    [Fact]
    public void Process_SeparateStreams_TrackIndependentState()
    {
        ActivityInterpreter interpreter = new();
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "first", "First status", null, StreamTypes.Informative, 1),
            ActivityDirection.Inbound);
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "second", "Second status", null, StreamTypes.Informative, 1),
            ActivityDirection.Inbound);

        IReadOnlyList<ChatChange> secondChanges = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "second-update", "Second answer", "second", StreamTypes.Streaming, 2),
            ActivityDirection.Inbound);
        IReadOnlyList<ChatChange> firstChanges = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "first-update", "First answer", "first", StreamTypes.Streaming, 2),
            ActivityDirection.Inbound);

        AssertAgentUpsert(secondChanges, "Second answer", "stream:second:response", transient: true);
        AssertAgentUpsert(firstChanges, "First answer", "stream:first:response", transient: true);
    }

    [Fact]
    public void Process_AmbiguousUpdateWithoutStreamIdentifier_AddsDiagnostic()
    {
        ActivityInterpreter interpreter = new();
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "first", "First status", null, StreamTypes.Informative, 1),
            ActivityDirection.Inbound);
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "second", "Second status", null, StreamTypes.Informative, 1),
            ActivityDirection.Inbound);

        IReadOnlyList<ChatChange> changes = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "update", "Unknown", null, StreamTypes.Streaming, 2),
            ActivityDirection.Inbound);

        AssertDiagnostic(changes, "unambiguous");
        Assert.DoesNotContain(changes, change => change.Entry?.Kind is ChatEntryKind.Agent or ChatEntryKind.Status);
    }

    [Fact]
    public void Process_StreamingText_ReplacesRatherThanAppends()
    {
        ActivityInterpreter interpreter = new();
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "s", "A brown", null, StreamTypes.Streaming, 1),
            ActivityDirection.Inbound);

        IReadOnlyList<ChatChange> changes = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "u", "A brown fox", "s", StreamTypes.Streaming, 2),
            ActivityDirection.Inbound);

        AssertAgentUpsert(changes, "A brown fox", "stream:s:response", transient: true);
    }

    [Fact]
    public void Process_SequenceRegression_AddsDiagnosticWithoutReplacingResponse()
    {
        ActivityInterpreter interpreter = new();
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "seed", "First", "s", StreamTypes.Streaming, 2),
            ActivityDirection.Inbound);

        IReadOnlyList<ChatChange> changes = interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "u", "Older", "s", StreamTypes.Streaming, 1),
            ActivityDirection.Inbound);

        AssertDiagnostic(changes, "sequence");
        Assert.DoesNotContain(changes, change => change.Entry?.Kind == ChatEntryKind.Agent);
    }

    [Fact]
    public void Process_StreamError_FinalizesVisibleTextAndAddsDiagnostic()
    {
        ActivityInterpreter interpreter = new();
        interpreter.Process(
            StreamActivity(ActivityTypes.Typing, "start-1", "Working", null, StreamTypes.Streaming, 1),
            ActivityDirection.Inbound);

        IReadOnlyList<ChatChange> changes = interpreter.Process(
            StreamActivity(ActivityTypes.Message, "final-1", "Sorry, this failed.", "start-1", StreamTypes.Final, null, StreamResults.Error),
            ActivityDirection.Inbound);

        AssertAgentUpsert(changes, "Sorry, this failed.", "stream:start-1:response", transient: false);
        int upsertIndex = changes.ToList().FindIndex(change => change.Entry?.Kind == ChatEntryKind.Agent);
        int diagnosticIndex = changes.ToList().FindIndex(change => change.Entry?.Kind == ChatEntryKind.Diagnostic);
        Assert.True(upsertIndex >= 0 && diagnosticIndex > upsertIndex);
    }

    private static Activity CreateMessageWithCard(string cardJson)
    {
        return new Activity
        {
            Type = ActivityTypes.Message,
            Attachments =
            [
                new Attachment
                {
                    ContentType = ContentTypes.AdaptiveCard,
                    Content = cardJson
                }
            ]
        };
    }

    private static Activity StreamActivity(
        string activityType,
        string activityId,
        string text,
        string? streamId,
        string streamType,
        int? sequence,
        string? streamResult = null)
    {
        return new Activity
        {
            Type = activityType,
            Id = activityId,
            Text = text,
            Entities =
            [
                new StreamInfo
                {
                    StreamId = streamId!,
                    StreamType = streamType,
                    StreamSequence = sequence,
                    StreamResult = streamResult!
                }
            ]
        };
    }

    private static void AssertStatus(IReadOnlyList<ChatChange> changes, string text, string key)
    {
        ChatChange change = Assert.Single(changes, item => item.Entry?.Kind == ChatEntryKind.Status);
        Assert.Equal(ChatChangeKind.Upsert, change.Kind);
        Assert.Equal(key, change.Key);
        Assert.Equal(text, change.Entry!.Text);
    }

    private static void AssertAgentUpsert(IReadOnlyList<ChatChange> changes, string text, string key, bool transient)
    {
        ChatChange change = Assert.Single(changes, item => item.Entry?.Kind == ChatEntryKind.Agent);
        Assert.Equal(ChatChangeKind.Upsert, change.Kind);
        Assert.Equal(key, change.Key);
        Assert.Equal(text, change.Entry!.Text);
        Assert.Equal(transient, change.Entry.IsTransient);
    }

    private static void AssertDiagnostic(IReadOnlyList<ChatChange> changes, string textFragment)
    {
        Assert.Contains(
            changes,
            change => change.Entry?.Kind == ChatEntryKind.Diagnostic
                && change.Entry.Text.Contains(textFragment, StringComparison.OrdinalIgnoreCase));
    }
}
