using System;
using System.Linq;

public sealed class TerminalChatStateTests
{
    [Fact]
    public void Apply_ReplacesExistingKeyInPlaceAndRemovesTransientEntry()
    {
        TerminalChatState state = new();
        ChatEntry first = Entry("first", "First");
        ChatEntry transient = Entry("status", "Working", isTransient: true);

        state.Apply(
        [
            new ChatChange(ChatChangeKind.Upsert, first.Key, first),
            new ChatChange(ChatChangeKind.Upsert, transient.Key, transient)
        ]);
        state.Apply(
        [
            new ChatChange(ChatChangeKind.Upsert, first.Key, Entry("first", "Updated")),
            new ChatChange(ChatChangeKind.Remove, transient.Key, null)
        ]);

        ChatEntry remaining = Assert.Single(state.Entries);
        Assert.Equal("first", remaining.Key);
        Assert.Equal("Updated", remaining.Text);
    }

    [Fact]
    public void Apply_FilterExcludesEntriesOutsidePredicate()
    {
        TerminalChatState state = new(entry => entry.Kind == ChatEntryKind.Thought);

        state.Apply(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "entry",
                new ChatEntry(
                    "entry",
                    ChatEntryKind.Agent,
                    "Agent",
                    "Final answer",
                    false,
                    [],
                    [],
                    "entry")),
            new ChatChange(
                ChatChangeKind.Upsert,
                "thought",
                new ChatEntry(
                    "thought",
                    ChatEntryKind.Thought,
                    "Reasoning",
                    "Checking account",
                    true,
                    [],
                    [],
                    "thought"))
        ]);

        ChatEntry thought = Assert.Single(state.Entries);
        Assert.Equal("thought", thought.Key);
        Assert.Equal("Checking account", thought.Text);
    }

    [Fact]
    public void ChatAndThoughtStates_RouteToolCallOnlyToThoughts()
    {
        TerminalChatState chat = new(
            entry => entry.Kind is not ChatEntryKind.Thought and not ChatEntryKind.ToolCall);
        TerminalChatState thoughts = new(
            entry => entry.Kind is ChatEntryKind.Thought or ChatEntryKind.ToolCall);
        ChatChange change = new(ChatChangeKind.Upsert, "tool:1", ToolEntry("1", "started"));

        chat.Apply([change]);
        thoughts.Apply([change]);

        Assert.Empty(chat.Entries);
        ChatEntry thought = Assert.Single(thoughts.Entries);
        Assert.Equal("tool:1", thought.Key);
        Assert.Equal(ChatEntryKind.ToolCall, thought.Kind);
    }

    [Fact]
    public void Apply_ReplacingEntryRemovesItsOldLinksAndActions()
    {
        TerminalChatState state = new();
        state.Apply(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "entry",
                new ChatEntry(
                    "entry",
                    ChatEntryKind.Agent,
                    "Agent",
                    "First",
                    false,
                    [new ChatLink("Old", new Uri("https://old.example"))],
                    [new ChatAction("Old action", "old")],
                    "entry"))
        ]);

        state.Apply(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "entry",
                new ChatEntry(
                    "entry",
                    ChatEntryKind.Agent,
                    "Agent",
                    "Updated",
                    false,
                    [new ChatLink("New", new Uri("https://new.example"))],
                    [new ChatAction("New action", "new")],
                    "entry"))
        ]);

        Assert.Equal("https://new.example/", Assert.Single(state.Links).Url.AbsoluteUri);
        Assert.Equal("new", Assert.Single(state.SuggestedActions).Value);
    }

    [Fact]
    public void Apply_SeparateEntriesInSameActionGroupAggregateLinksAndActions()
    {
        TerminalChatState state = new();
        state.Apply(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "message",
                new ChatEntry(
                    "message",
                    ChatEntryKind.Agent,
                    "Agent",
                    "Choose",
                    false,
                    [],
                    [new ChatAction("Continue", "continue")],
                    "response-1"))
        ]);
        state.Apply(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "attachment-1",
                new ChatEntry(
                    "attachment-1",
                    ChatEntryKind.Attachment,
                    "Agent",
                    "First",
                    false,
                    [new ChatLink("First", new Uri("https://first.example"))],
                    [],
                    "response-1")),
            new ChatChange(
                ChatChangeKind.Upsert,
                "attachment-2",
                new ChatEntry(
                    "attachment-2",
                    ChatEntryKind.Attachment,
                    "Agent",
                    "Second",
                    false,
                    [new ChatLink("Second", new Uri("https://second.example"))],
                    [],
                    "response-1"))
        ]);

        Assert.Equal(
            ["https://first.example/", "https://second.example/"],
            state.Links.Select(link => link.Url.AbsoluteUri));
        Assert.Equal("continue", Assert.Single(state.SuggestedActions).Value);
    }

    [Fact]
    public void Apply_NewerActionableEntryReplacesCurrentLinksAndActions()
    {
        TerminalChatState state = new();
        state.Apply(
        [
            ActionableEntry("first", "Old", "https://old.example", "Old action", "old")
        ]);

        state.Apply(
        [
            ActionableEntry("second", "New", "https://new.example", "New action", "new")
        ]);

        Assert.Equal("https://new.example/", Assert.Single(state.Links).Url.AbsoluteUri);
        Assert.Equal("new", Assert.Single(state.SuggestedActions).Value);
    }

    [Fact]
    public void Apply_NonActionableEntryFromNewGroupPreservesCurrentLinksAndActions()
    {
        TerminalChatState state = new();
        state.Apply(
        [
            ActionableEntry("first", "Old", "https://old.example", "Old action", "old")
        ]);

        state.Apply(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "status",
                new ChatEntry(
                    "status",
                    ChatEntryKind.Status,
                    "Agent",
                    "Working",
                    true,
                    [],
                    [],
                    "response-2"))
        ]);

        Assert.Equal("https://old.example/", Assert.Single(state.Links).Url.AbsoluteUri);
        Assert.Equal("old", Assert.Single(state.SuggestedActions).Value);
    }

    [Fact]
    public void ClearActions_RemovesCurrentLinksAndSuggestedActions()
    {
        TerminalChatState state = new();
        state.Apply(
        [
            ActionableEntry("entry", "Choose", "https://example.com", "Choose action", "choose")
        ]);

        state.ClearActions();

        Assert.Empty(state.Links);
        Assert.Empty(state.SuggestedActions);
    }

    private static ChatEntry Entry(string key, string text, bool isTransient = false)
    {
        return new ChatEntry(key, ChatEntryKind.Agent, "Agent", text, isTransient, [], [], key);
    }

    private static ChatChange ActionableEntry(
        string key,
        string text,
        string url,
        string actionTitle,
        string actionValue)
    {
        return new ChatChange(
            ChatChangeKind.Upsert,
            key,
            new ChatEntry(
                key,
                ChatEntryKind.Agent,
                "Agent",
                text,
                false,
                [new ChatLink(text, new Uri(url))],
                [new ChatAction(actionTitle, actionValue)],
                key));
    }

    private static ChatEntry ToolEntry(string id, string status) =>
        new(
            $"tool:{id}",
            ChatEntryKind.ToolCall,
            "Agent",
            string.Empty,
            !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase),
            [],
            [],
            "stream-1",
            ToolCall: new ToolCallDetails(
                id,
                "current_weather",
                "Get current weather",
                "Connector",
                status,
                [],
                [],
                null));
}
