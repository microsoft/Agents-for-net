internal enum ChatEntryKind
{
    User,
    Agent,
    Status,
    Event,
    Thought,
    Attachment,
    Diagnostic
}

internal sealed record ChatLink(string Title, Uri Url);

internal sealed record ChatAction(string Title, string? Value);

internal sealed record ChatEntry(
    string Key,
    ChatEntryKind Kind,
    string Author,
    string Text,
    bool IsTransient,
    IReadOnlyList<ChatLink> Links,
    IReadOnlyList<ChatAction> SuggestedActions);

internal enum ChatChangeKind
{
    Upsert,
    Remove
}

internal sealed record ChatChange(ChatChangeKind Kind, string Key, ChatEntry? Entry);

internal sealed record LinkExtractionResult(
    IReadOnlyList<ChatLink> Links,
    string? Error);
