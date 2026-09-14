using System.Text.Json;
using System.Threading;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

internal sealed class ActivityInterpreter
{
    private const int ClosedStreamIdCapacity = 256;

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record OpenStream(
        string Id,
        string ResponseKey,
        int? LastSequence,
        string ResponseText,
        Dictionary<string, string> ThoughtTexts);

    private readonly Dictionary<string, OpenStream> _streams =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> _closedStreamIds =
        new(StringComparer.Ordinal);

    private readonly Queue<string> _closedStreamIdOrder = new();

    private long _nextSyntheticId;

    public IReadOnlyList<ChatChange> Process(Activity activity, ActivityDirection direction)
    {
        ArgumentNullException.ThrowIfNull(activity);

        StreamInfo? streamInfo = activity.GetStreamingEntity();
        if (streamInfo is not null)
        {
            return ProcessStream(activity, direction, streamInfo);
        }

        string activityIdentity = GetActivityIdentity(activity);
        List<ChatChange> changes = [];
        AddOrdinaryEntry(changes, activity, direction, activityIdentity);
        AddThoughtEntries(changes, activity, direction, activityIdentity);
        AddToolCallEntries(changes, activity, direction, activityIdentity);
        AddAttachmentEntries(changes, activity, direction, activityIdentity);
        return changes;
    }

    private IReadOnlyList<ChatChange> ProcessStream(Activity activity, ActivityDirection direction, StreamInfo streamInfo)
    {
        string activityIdentity = GetActivityIdentity(activity);
        bool isInformative = string.Equals(streamInfo.StreamType, StreamTypes.Informative, StringComparison.OrdinalIgnoreCase);
        bool isFinal = string.Equals(streamInfo.StreamType, StreamTypes.Final, StringComparison.OrdinalIgnoreCase);
        List<ChatChange> changes = [];
        IReadOnlyList<ChatAction> suggestedActions = GetSuggestedActions(activity);
        bool isValidStart = IsValidStreamStart(streamInfo, isInformative, isFinal);
        string? streamId = ResolveStreamId(activity, streamInfo, isValidStart);
        bool mutatedStream = false;

        if (string.IsNullOrWhiteSpace(streamId))
        {
            changes.Add(CreateDiagnosticChange(
                $"activity:{activityIdentity}:diagnostic:stream",
                "Streaming activity has no unambiguous stream identifier.",
                activityIdentity,
                suggestedActions));
        }
        else
        {
            _streams.TryGetValue(streamId, out OpenStream? openStream);
            string responseKey = openStream?.ResponseKey ?? $"stream:{streamId}:response";
            string statusKey = $"stream:{streamId}:status";
            ChatEntryKind responseKind = GetMessageKind(direction);
            string responseText = openStream?.ResponseText ?? string.Empty;
            Dictionary<string, string> thoughtTexts =
                openStream?.ThoughtTexts ?? new Dictionary<string, string>(StringComparer.Ordinal);

            if (openStream is null
                && _closedStreamIds.Contains(streamId))
            {
                changes.Add(CreateDiagnosticChange(
                    $"stream:{streamId}:diagnostic:{NextSyntheticIdentity()}",
                    $"Streaming activity targeted closed stream '{streamId}'.",
                    activityIdentity,
                    suggestedActions));
            }
            else if (openStream is null && !isValidStart)
            {
                changes.Add(CreateDiagnosticChange(
                    $"stream:{streamId}:diagnostic:{NextSyntheticIdentity()}",
                    $"Streaming activity targeted stream '{streamId}', which is not open.",
                    activityIdentity,
                    suggestedActions));
            }
            else if (!isFinal
                && openStream is not null
                && streamInfo.StreamSequence is int sequence
                && openStream.LastSequence is int lastSequence
                && sequence <= lastSequence)
            {
                changes.Add(CreateDiagnosticChange(
                    $"stream:{streamId}:diagnostic:{NextSyntheticIdentity()}",
                    $"Streaming activity sequence {sequence} did not advance stream '{streamId}'.",
                    activityIdentity,
                    suggestedActions));
            }
            else
            {
                AddStreamingThoughtEntries(
                    changes,
                    activity,
                    direction,
                    streamId,
                    thoughtTexts,
                    isTransient: !isFinal);

                if (isInformative)
                {
                    changes.Add(CreateEntryChange(
                        statusKey,
                        ChatEntryKind.Status,
                        GetAuthor(activity, ChatEntryKind.Status, direction),
                        GetMessageText(activity),
                        isTransient: true,
                        links: [],
                        suggestedActions,
                        activityIdentity));
                }
                else
                {
                    string messageText = GetMessageText(activity);
                    responseText = !isFinal
                        && string.Equals(activity.Type, ActivityTypes.Typing, StringComparison.OrdinalIgnoreCase)
                            ? responseText + messageText
                            : messageText;
                    changes.Add(new ChatChange(ChatChangeKind.Remove, statusKey, null));
                    if (!string.IsNullOrEmpty(messageText) || isFinal)
                    {
                        changes.Add(CreateEntryChange(
                            responseKey,
                            responseKind,
                            GetAuthor(activity, responseKind, direction),
                            responseText,
                            isTransient: !isFinal,
                            links: [],
                            suggestedActions,
                            activityIdentity));
                    }
                }

                if (isFinal)
                {
                    FinalizeStreamingThoughts(changes, activity, direction, streamId, thoughtTexts);

                    _streams.Remove(streamId);
                    RememberClosedStreamId(streamId);
                }
                else
                {
                    int? updatedSequence = streamInfo.StreamSequence ?? openStream?.LastSequence;
                    _streams[streamId] = new OpenStream(
                        streamId,
                        responseKey,
                        updatedSequence,
                        responseText,
                        thoughtTexts);
                }

                mutatedStream = true;
            }
        }

        if (!mutatedStream)
        {
            AddThoughtEntries(changes, activity, direction, activityIdentity);
        }

        AddToolCallEntries(changes, activity, direction, activityIdentity);
        AddAttachmentEntries(changes, activity, direction, activityIdentity);

        if (mutatedStream
            && !string.IsNullOrWhiteSpace(streamId)
            && string.Equals(streamInfo.StreamResult, StreamResults.Error, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateDiagnosticChange(
                $"stream:{streamId}:diagnostic:{NextSyntheticIdentity()}",
                $"Stream '{streamId}' completed with an error result.",
                activityIdentity,
                severity: DiagnosticSeverity.Error));
        }

        return changes;
    }

    private string? ResolveStreamId(Activity activity, StreamInfo streamInfo, bool isValidStart)
    {
        if (!string.IsNullOrWhiteSpace(streamInfo.StreamId))
        {
            return streamInfo.StreamId;
        }

        if (isValidStart && !string.IsNullOrWhiteSpace(activity.Id))
        {
            return activity.Id;
        }

        if (!isValidStart && _streams.Count == 1)
        {
            return _streams.Keys.Single();
        }

        return null;
    }

    private static bool IsValidStreamStart(StreamInfo streamInfo, bool isInformative, bool isFinal)
    {
        if (isFinal)
        {
            return false;
        }

        if (streamInfo.StreamSequence == 1)
        {
            return true;
        }

        return isInformative
            && streamInfo.StreamSequence is null;
    }

    private void RememberClosedStreamId(string streamId)
    {
        if (!_closedStreamIds.Add(streamId))
        {
            return;
        }

        _closedStreamIdOrder.Enqueue(streamId);
        while (_closedStreamIdOrder.Count > ClosedStreamIdCapacity)
        {
            string oldestStreamId = _closedStreamIdOrder.Dequeue();
            _closedStreamIds.Remove(oldestStreamId);
        }
    }

    private void AddToolCallEntries(List<ChatChange> changes, Activity activity, ActivityDirection direction, string actionGroupKey)
    {
        if (activity.Entities is null || activity.Entities.Count == 0)
        {
            return;
        }

        foreach (Entity entity in activity.Entities)
        {
            if (!IsToolCallEntity(entity))
            {
                continue;
            }

            ToolCallDetails? details = ParseToolCall(entity, out string? error);
            if (details is null)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    changes.Add(CreateDiagnosticChange(
                        $"{actionGroupKey}:diagnostic:toolcall:{NextSyntheticIdentity()}",
                        error!,
                        actionGroupKey));
                }

                continue;
            }

            string key = $"tool:{details.Id}";
            changes.Add(new ChatChange(
                ChatChangeKind.Upsert,
                key,
                new ChatEntry(
                    key,
                    ChatEntryKind.ToolCall,
                    GetAuthor(activity, ChatEntryKind.ToolCall, direction),
                    string.Empty,
                    IsTransientToolStatus(details.Status),
                    [],
                    [],
                    actionGroupKey,
                    ToolCall: details)));
        }
    }

    private void AddOrdinaryEntry(List<ChatChange> changes, Activity activity, ActivityDirection direction, string activityIdentity)
    {
        ChatEntryKind? kind = activity.Type switch
        {
            var type when string.Equals(type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase) => GetMessageKind(direction),
            var type when string.Equals(type, ActivityTypes.Typing, StringComparison.OrdinalIgnoreCase) => ChatEntryKind.Status,
            var type when string.Equals(type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase) => ChatEntryKind.Event,
            _ => null
        };

        if (kind is null)
        {
            return;
        }

        if (kind is ChatEntryKind.User or ChatEntryKind.Agent
            && IsToolCallOnlyMessage(activity))
        {
            return;
        }

        string key = $"activity:{activityIdentity}:entry";
        changes.Add(CreateEntryChange(
            key,
            kind.Value,
            GetAuthor(activity, kind.Value, direction),
            GetTextForKind(activity, kind.Value),
            isTransient: kind == ChatEntryKind.Status,
            links: [],
            suggestedActions: GetSuggestedActions(activity),
            actionGroupKey: activityIdentity));
    }

    private void AddThoughtEntries(List<ChatChange> changes, Activity activity, ActivityDirection direction, string activityIdentity)
    {
        if (activity.Entities is null || activity.Entities.Count == 0)
        {
            return;
        }

        int thoughtIndex = 0;
        foreach (Entity entity in activity.Entities)
        {
            if (!string.Equals(entity.Type, "thought", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entity.Type, "thoughts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string key = $"activity:{activityIdentity}:thought:{thoughtIndex++}";
            string text = GetThoughtText(entity);
            changes.Add(CreateEntryChange(
                key,
                ChatEntryKind.Thought,
                GetAuthor(activity, ChatEntryKind.Thought, direction),
                text,
                isTransient: false,
                links: [],
                suggestedActions: [],
                actionGroupKey: activityIdentity));
        }
    }

    private void AddAttachmentEntries(List<ChatChange> changes, Activity activity, ActivityDirection direction, string activityIdentity)
    {
        if (activity.Attachments is null || activity.Attachments.Count == 0)
        {
            return;
        }

        for (int index = 0; index < activity.Attachments.Count; index++)
        {
            Attachment attachment = activity.Attachments[index];
            IReadOnlyList<ChatLink> links = [];
            string? extractionError = null;

            if (string.Equals(attachment.ContentType, ContentTypes.AdaptiveCard, StringComparison.OrdinalIgnoreCase))
            {
                LinkExtractionResult extraction = AdaptiveCardLinkExtractor.Extract(attachment.Content);
                links = extraction.Links;
                extractionError = extraction.Error;
            }

            string key = $"activity:{activityIdentity}:attachment:{index}";
            changes.Add(CreateEntryChange(
                key,
                ChatEntryKind.Attachment,
                GetAuthor(activity, ChatEntryKind.Attachment, direction),
                DescribeAttachment(attachment),
                isTransient: false,
                links,
                suggestedActions: [],
                actionGroupKey: activityIdentity));

            if (!string.IsNullOrWhiteSpace(extractionError))
            {
                changes.Add(CreateDiagnosticChange(
                    $"{key}:diagnostic",
                    $"Unable to extract adaptive card links: {extractionError}",
                    activityIdentity));
            }
        }
    }

    private ChatChange CreateEntryChange(
        string key,
        ChatEntryKind kind,
        string author,
        string text,
        bool isTransient,
        IReadOnlyList<ChatLink> links,
        IReadOnlyList<ChatAction> suggestedActions,
        string actionGroupKey)
    {
        return new ChatChange(
            ChatChangeKind.Upsert,
            key,
            new ChatEntry(
                key,
                kind,
                author,
                text,
                isTransient,
                links,
                suggestedActions,
                actionGroupKey));
    }

    private static ChatChange CreateDiagnosticChange(
        string key,
        string message,
        string actionGroupKey,
        IReadOnlyList<ChatAction>? suggestedActions = null,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning)
    {
        return new ChatChange(
            ChatChangeKind.Upsert,
            key,
            new ChatEntry(
                key,
                ChatEntryKind.Diagnostic,
                "System",
                message,
                false,
                [],
                suggestedActions ?? [],
                actionGroupKey,
                severity));
    }

    private static ChatEntryKind GetMessageKind(ActivityDirection direction)
    {
        return direction == ActivityDirection.Outbound
            ? ChatEntryKind.User
            : ChatEntryKind.Agent;
    }

    private static string GetAuthor(Activity activity, ChatEntryKind kind, ActivityDirection direction)
    {
        if (!string.IsNullOrWhiteSpace(activity.From?.Name))
        {
            return activity.From.Name;
        }

        if (!string.IsNullOrWhiteSpace(activity.From?.Id))
        {
            return activity.From.Id;
        }

        return kind switch
        {
            ChatEntryKind.User => "You",
            ChatEntryKind.Agent => "Agent",
            ChatEntryKind.Status => direction == ActivityDirection.Outbound ? "You" : "Agent",
            ChatEntryKind.Event => "System",
            ChatEntryKind.Thought => direction == ActivityDirection.Outbound ? "You" : "Agent",
            ChatEntryKind.ToolCall => direction == ActivityDirection.Outbound ? "You" : "Agent",
            ChatEntryKind.Attachment => direction == ActivityDirection.Outbound ? "You" : "Agent",
            ChatEntryKind.Diagnostic => "System",
            _ => "System"
        };
    }

    private static string GetTextForKind(Activity activity, ChatEntryKind kind)
    {
        return kind switch
        {
            ChatEntryKind.User or ChatEntryKind.Agent => GetMessageText(activity),
            ChatEntryKind.Status or ChatEntryKind.Event => GetStatusOrEventText(activity),
            _ => GetStatusOrEventText(activity)
        };
    }

    private static string GetMessageText(Activity activity)
    {
        return activity.Text
            ?? activity.Summary
            ?? string.Empty;
    }

    private static bool IsToolCallOnlyMessage(Activity activity)
    {
        return string.Equals(activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(activity.Text)
            && string.IsNullOrWhiteSpace(activity.Summary)
            && activity.Entities is { Count: > 0 }
            && activity.Entities.All(IsToolCallEntity);
    }

    private static string GetStatusOrEventText(Activity activity)
    {
        if (!string.IsNullOrWhiteSpace(activity.Text))
        {
            return activity.Text;
        }

        if (!string.IsNullOrWhiteSpace(activity.Summary))
        {
            return activity.Summary;
        }

        if (!string.IsNullOrWhiteSpace(activity.Name))
        {
            return activity.Name;
        }

        if (!string.IsNullOrWhiteSpace(activity.Type))
        {
            return activity.Type;
        }

        return "activity";
    }

    private static IReadOnlyList<ChatAction> GetSuggestedActions(Activity activity)
    {
        if (activity.SuggestedActions?.Actions is null || activity.SuggestedActions.Actions.Count == 0)
        {
            return [];
        }

        return activity.SuggestedActions.Actions
            .Select(action => new ChatAction(
                !string.IsNullOrWhiteSpace(action.Text)
                    ? action.Text
                    : action.Title ?? string.Empty,
                action.Value?.ToString()))
            .ToArray();
    }

    private static string GetThoughtText(Entity entity)
    {
        foreach (string propertyName in new[] { "text", "content", "description", "value" })
        {
            if (TryGetStringProperty(entity, propertyName, out string? value))
            {
                return value!;
            }
        }

        JsonElement serialized = JsonSerializer.SerializeToElement(
            entity,
            entity.GetType(),
            ProtocolJsonSerializer.SerializationOptions);
        return JsonSerializer.Serialize(serialized, IndentedJsonOptions);
    }

    private void AddStreamingThoughtEntries(
        List<ChatChange> changes,
        Activity activity,
        ActivityDirection direction,
        string streamId,
        Dictionary<string, string> thoughtTexts,
        bool isTransient)
    {
        if (activity.Entities is null)
        {
            return;
        }

        foreach (Entity entity in activity.Entities)
        {
            if (!IsThoughtEntity(entity))
            {
                continue;
            }

            string chainId = TryGetStringProperty(entity, "chainOfThoughtId", out string? value)
                && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : "default";
            string key = $"stream:{streamId}:thought:{chainId}";
            thoughtTexts.TryGetValue(key, out string? currentText);
            string text = (currentText ?? string.Empty) + GetThoughtText(entity);
            thoughtTexts[key] = text;
            changes.Add(CreateEntryChange(
                key,
                ChatEntryKind.Thought,
                GetAuthor(activity, ChatEntryKind.Thought, direction),
                text,
                isTransient,
                links: [],
                suggestedActions: [],
                actionGroupKey: streamId));
        }
    }

    private void FinalizeStreamingThoughts(
        List<ChatChange> changes,
        Activity activity,
        ActivityDirection direction,
        string streamId,
        IReadOnlyDictionary<string, string> thoughtTexts)
    {
        foreach (KeyValuePair<string, string> thought in thoughtTexts)
        {
            if (changes.Any(change =>
                change.Key == thought.Key
                && change.Entry?.Kind == ChatEntryKind.Thought
                && !change.Entry.IsTransient))
            {
                continue;
            }

            changes.Add(CreateEntryChange(
                thought.Key,
                ChatEntryKind.Thought,
                GetAuthor(activity, ChatEntryKind.Thought, direction),
                thought.Value,
                isTransient: false,
                links: [],
                suggestedActions: [],
                actionGroupKey: streamId));
        }
    }

    private static bool IsThoughtEntity(Entity entity)
    {
        return string.Equals(entity.Type, "thought", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Type, "thoughts", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsToolCallEntity(Entity entity)
    {
        return string.Equals(entity.Type, "toolCall", StringComparison.OrdinalIgnoreCase);
    }

    private static ToolCallDetails? ParseToolCall(Entity entity, out string? error)
    {
        string id = GetOptionalString(entity, "toolCallId")?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            error = "Tool call entity has no toolCallId.";
            return null;
        }

        string name = GetOptionalString(entity, "toolName")
            ?? GetOptionalString(entity, "toolDisplayName")
            ?? "tool";
        string? displayName = GetOptionalString(entity, "toolDisplayName");
        string? category = GetOptionalString(entity, "toolCategory");
        string status = GetOptionalString(entity, "status") ?? "unknown";
        long? durationMs = TryGetNonnegativeInt64(entity, "durationMs");
        IReadOnlyList<ToolCallParameter> filledParameters = ParseFilledParameters(entity);
        IReadOnlyList<string> unfilledParameters = ParseUnfilledParameters(entity);

        error = null;
        return new ToolCallDetails(
            id,
            name,
            displayName,
            category,
            status,
            filledParameters,
            unfilledParameters,
            durationMs);
    }

    private static IReadOnlyList<ToolCallParameter> ParseFilledParameters(Entity entity)
    {
        if (!TryGetProperty(entity, "filledParameters", out JsonElement value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            List<ToolCallParameter> parameters = [];
            foreach (JsonProperty property in value.EnumerateObject())
            {
                parameters.Add(new ToolCallParameter(property.Name, property.Value.Clone()));
            }

            return parameters;
        }

        return [new ToolCallParameter("parameters", value.Clone())];
    }

    private static IReadOnlyList<string> ParseUnfilledParameters(Entity entity)
    {
        if (!TryGetProperty(entity, "unfilledParameters", out JsonElement value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            List<string> parameters = [];
            foreach (JsonElement item in value.EnumerateArray())
            {
                parameters.Add(item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : JsonSerializer.Serialize(item, ProtocolJsonSerializer.SerializationOptions));
            }

            return parameters;
        }

        return [JsonSerializer.Serialize(value, ProtocolJsonSerializer.SerializationOptions)];
    }

    private static bool IsTransientToolStatus(string status)
    {
        return !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetStringProperty(Entity entity, string propertyName, out string? value)
    {
        foreach (KeyValuePair<string, JsonElement> property in entity.Properties)
        {
            if (!string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString();
                return value is not null;
            }

            break;
        }

        value = null;
        return false;
    }

    private static string? GetOptionalString(Entity entity, string propertyName)
    {
        return TryGetStringProperty(entity, propertyName, out string? value)
            ? value
            : null;
    }

    private static bool TryGetProperty(Entity entity, string propertyName, out JsonElement value)
    {
        foreach (KeyValuePair<string, JsonElement> property in entity.Properties)
        {
            if (!string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static long? TryGetNonnegativeInt64(Entity entity, string propertyName)
    {
        if (!TryGetProperty(entity, propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out long result)
            || result < 0)
        {
            return null;
        }

        return result;
    }

    private static string DescribeAttachment(Attachment attachment)
    {
        List<string> details = [];

        if (!string.IsNullOrWhiteSpace(attachment.Name))
        {
            details.Add($"Name: {attachment.Name}");
        }

        if (!string.IsNullOrWhiteSpace(attachment.ContentType))
        {
            string type = string.Equals(attachment.ContentType, ContentTypes.AdaptiveCard, StringComparison.OrdinalIgnoreCase)
                ? $"Adaptive card ({attachment.ContentType})"
                : attachment.ContentType;
            details.Add($"Type: {type}");
        }

        if (!string.IsNullOrWhiteSpace(attachment.ContentUrl))
        {
            details.Add($"URL: {attachment.ContentUrl}");
        }

        return details.Count == 0
            ? "Attachment"
            : string.Join("; ", details);
    }

    private string GetActivityIdentity(Activity activity)
    {
        return !string.IsNullOrWhiteSpace(activity.Id)
            ? activity.Id
            : NextSyntheticIdentity();
    }

    private string NextSyntheticIdentity()
    {
        return $"synthetic:{Interlocked.Increment(ref _nextSyntheticId)}";
    }
}
