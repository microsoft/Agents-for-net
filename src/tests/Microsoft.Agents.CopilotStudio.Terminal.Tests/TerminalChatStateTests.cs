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
    public void Markdown_EscapesReceivedFormattingAndControlCharacters()
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
                    "# Agent",
                    "**bold** [unsafe](https://example.com)\u001b",
                    false,
                    [],
                    [],
                    "entry"))
        ]);

        Assert.Equal(
            "## \\# Agent\r\n\r\n\\*\\*bold\\*\\* \\[unsafe\\]\\(https://example\\.com\\)\\u001B",
            state.Markdown);
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
}
