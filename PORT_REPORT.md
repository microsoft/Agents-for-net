# Case Study: Porting Teams SDK features into the Agents SDK Microsoft Teams extension

This document goes through several features present in the Teams SDK and discusses the process of porting those over into the Microsoft Teams extension in the Agents SDK. We will focus on Teams.NET and Agents SDK for .NET, but the JS and Python Agents SDKs are similar enough that the implementation in those are unlikely to stray too far from the .NET implementation.

This document relies on Teams.net 2.0.9, as 2.1.0 has some major API differences, and we have not yet migrated our SDK to use 2.1.0 yet.

> **Note:** The APIs covering the ported features in this document are not final and are subject to change. The intention of this document is to illustrate the ease that comes from porting the implementation of a feature rather than the API design.

---

## Case I - Targeted Messages

This feature is already present across .NET, JS, and Python Agents SDKs.

### Code

```csharp
// ActivityTreatment.cs
public class ActivityTreatment : Entity
{
    public ActivityTreatment() : base(EntityTypes.ActivityTreatment)
    {
    }

    public string Treatment { get; set; }
}


// Activity.cs
public IActivity MakeTargetedActivity(ChannelAccount user = null)
{
    if (IsTargetedActivity())
    {
        return this;
    }

    if (Recipient == null && user == null)
    {
        throw new InvalidOperationException("Cannot mark activity as targeted because both the Activity.Recipient and `user` argument are null. At least one must be provided.");
    }

    Entities ??= [];
    Entities.Add(new ActivityTreatment() { Treatment = "targeted" });

    Recipient = user ?? Recipient;

    return this;
}

// TeamsTurnContext.cs
public Task<ResourceResponse> SendTargetedActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
{
    return SendActivityAsync(activity.Clone().MakeTargetedActivity(), cancellationToken);
}
```

### Conclusion

This feature required a new entity type, a fluent helper for marking an activity as targeted, and a method on the Teams turn context to send the targeted activity. The implementation mapped directly onto existing Activity Protocol concepts.

---

## Case II - Reactions

### Initial Prompt

```text
Hi Copilot. I want you to take a look at how the Teams.net's ApiClient via the ConversationApiClient adds reactions to activities here: https://github.com/microsoft/teams.net/blob/main/src/Microsoft.Teams.Apps/Clients/ConversationApiClient.cs.

Using this, please port in new methods to TeamsTurnContext AddReactionAsync(string reactionType, string activityId? = null) and DeleteReactionAsync(string reactionType, string activityId? = null), where if not set, we resolve to the current activity.
```

### Code

Since our `ITeamsTurnContext` already holds a `ApiClient`, the change was very straightforward, only requiring two simple methods.

```csharp
// TeamsTurnContext.cs
public Task AddReactionAsync(string reactionType, string? activityId = null)
{
    return Client.Conversations.Reactions.AddAsync(
        Activity.Conversation.Id,
        activityId ?? Activity.Id,
        new Microsoft.Teams.Api.Messages.ReactionType(reactionType));
}

public Task DeleteReactionAsync(string reactionType, string? activityId = null)
{
    return Client.Conversations.Reactions.DeleteAsync(
        Activity.Conversation.Id,
        activityId ?? Activity.Id,
        new Microsoft.Teams.Api.Messages.ReactionType(reactionType));
}
```

### Conclusion

Adding this feature and the corresponding tests was very straightforward. All in all, it took less than 30 minutes.

---

## Case III - Quoted Replies

### Initial Prompt

This was in the same Copilot CLI session as for the feature II above, so we were a little less specific in the prompt:

```text
Hi Copilot, can you add the Reply/ReplyAsync functionality provided by Teams.net? I don't remember where it is anymore in 2.0.9, but use Context<TActivity> definition as a basis. I want the signature to be ReplyAsync(string message,...) and then ReplyAsync(Activity activity, ...), we should also add a prependQuote (is that what it's called?) and QuotedReply entity (is that what it is called?)
```

Copilot mostly did got it right, but we prompted it to remove a few unnecessary attributes JsonPropertyName attributes it added to the `QuotedReplyData` fields.

### Code

While more complex than the reactions port, this change was still relatively straightfoward. The implementation follows a similar pattern as the targeted messages feature. The main difference is that `PrependQuote` is added as an `Activity` extension rather than added as part of the`IActivity` interface. 

```csharp
// TeamsActivityExtension.cs
public static Activity PrependQuote(this Activity activity, string messageId)
{
    AssertionHelpers.ThrowIfNull(activity, nameof(activity));
    AssertionHelpers.ThrowIfNullOrWhiteSpace(messageId, nameof(messageId));

    activity.Entities ??= [];
    activity.Entities.Add(new QuotedReplyEntity
    {
        QuotedReply = new QuotedReplyData { MessageId = messageId }
    });

    var placeholder = $"<quoted messageId=\"{SecurityElement.Escape(messageId)}\"/>";
    activity.Text = string.IsNullOrWhiteSpace(activity.Text)
        ? placeholder
        : $"{placeholder} {activity.Text}";

    return activity;
}

// QuotedReplyEntity.cs
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Represents metadata for a Teams quoted reply.
/// </summary>
[EntityName(EntityName)]
public class QuotedReplyEntity : Entity
{
    public const string EntityName = "quotedReply";

    public QuotedReplyEntity() : base(EntityName)
    {
    }

    /// <summary>
    /// Gets or sets the quoted message metadata.
    /// </summary>
    [JsonPropertyOrder(3)]
    public required QuotedReplyData QuotedReply { get; set; }
}

/// <summary>
/// Contains metadata about the message referenced by a quoted reply.
/// </summary>
public class QuotedReplyData
{
    public required string MessageId { get; set; }

    public string? SenderId { get; set; }

    public string? SenderName { get; set; }

    public string? Preview { get; set; }

    public string? Time { get; set; }

    public bool? IsReplyDeleted { get; set; }

    public bool? ValidatedMessageReference { get; set; }
}

// TeamsTurnContext.cs
public Task<ResourceResponse> ReplyAsync(string message, CancellationToken cancellationToken = default)
{
    return ReplyAsync(new Activity { Type = ActivityTypes.Message, Text = message }, cancellationToken);
}

/// <inheritdoc/>
public Task<ResourceResponse> ReplyAsync(Activity activity, CancellationToken cancellationToken = default)
{
    if (Activity.Id != null)
    {
        activity.PrependQuote(Activity.Id);
    }

    return SendActivityAsync(activity, cancellationToken);
}
```

### Conclusion

Overall, the implementation and unit testing took less than 45 minutes.

---

## Case IV - Proactive Threading

### Prompt

```text
Look at the "Proactive Threading" here https://learn.microsoft.com/en-us/microsoftteams/platform/teams-sdk/essentials/sending-messages/proactive-messaging?tabs=minimal&pivots=csharp. Can you port the SendAsync functionality in https://github.com/microsoft/teams.net/blob/main/src/Microsoft.Teams.Apps/TeamsBotApplication.cs (TeamsBotApplication) into ITeamsTurnContext.SendAsync(string conversationId, string activityId, string text, ...)? and ITeamsTurnContext.SendAsync(string conversationId, string text,...). You should use app.Proactive here. This is meant to support the "Proactive Threading feature" presented here: https://learn.microsoft.com/en-us/microsoftteams/platform/teams-sdk/essentials/sending-messages/proactive-messaging?tabs=minimal&pivots=csharp
```

A couple of adjustments had to be made to the initial implementation. We had researched this feature before from looking at the Teams.net source, and we ended up manually implementing the `CreateProactiveConversation` method below.

### Code

The implementation takes advantage of the fact that our `TeamsTurnContext` holds a reference to the `Proactive` instance created by the `AgentApplication`. The helper `ToThreadedConversationId` seems to be a direct port of the Teams.net feature as well.

```csharp
// TeamsTurnContext.cs
public Task<ResourceResponse> SendAsync(string conversationId, string text, CancellationToken cancellationToken = default)
{
    var conversation = CreateProactiveConversation(conversationId);
    return Proactive.SendActivityAsync(
        Adapter,
        conversation,
        MessageFactory.Text(text),
        cancellationToken);
}

/// <inheritdoc/>
public async Task<ResourceResponse> SendAsync(string conversationId, string activityId, string text, CancellationToken cancellationToken = default)
{
    string threadedConversationId = ToThreadedConversationId(conversationId, activityId);
    var threadedConversation = CreateProactiveConversation(threadedConversationId);

    return await Proactive.SendActivityAsync(
        Adapter,
        threadedConversation,
        MessageFactory.Text(text),
        cancellationToken).ConfigureAwait(false);
}

private Conversation CreateProactiveConversation(string conversationId)
{
    AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

    var reference = new ConversationReference(
        user: Activity.From,
        agent: Activity.Recipient,
        channelId: Microsoft.Agents.Core.Models.Channels.Msteams,
        serviceUrl: Activity.ServiceUrl,
        conversation: new ConversationAccount(id: conversationId));
    return new Conversation(Identity, reference);
}

private static string ToThreadedConversationId(string conversationId, string activityId)
{
    AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));
    if (string.IsNullOrEmpty(activityId) || !ulong.TryParse(activityId, out ulong parsedActivityId) || parsedActivityId == 0)
    {
        throw new ArgumentException(
            $"Invalid activityId \"{activityId}\": must be a non-zero numeric value.",
            nameof(activityId));
    }

    string baseConversationId = conversationId.Split(';')[0];
    return $"{baseConversationId};messageid={activityId}";
}
```

### Conclusion

This feature took less than two hours to port, as the heavy lifting was done by our existing `Proactive` class and related functionality. A significant chunk of the time had to do with testing the feature end-to-end in Teams.

---

## Overall Conclusion

These case studies show that Teams SDK features can often be ported into the Agents SDK Microsoft Teams extension by mapping them onto existing Activity Protocol models, turn context APIs, and proactive messaging infrastructure. Each feature required relatively little implementation time, with the reactions, quoted replies, and proactive threading ports each completed within 1-2 hours, requiring less than a day's work.