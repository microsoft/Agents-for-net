# Microsoft.Agents.Extensions.MSTeams

## About

Provides Microsoft Teams-specific extensions for the Microsoft 365 Agents SDK. Includes the Teams AgentExtension for accessing Teams context, roster, and meeting information within agent turns.

## Main Types

- TeamsAgentExtension: Provides access to Microsoft Teams context, roster, and meeting information within agent turns.
- TeamsTurnContext: Represents the context of a turn in a Microsoft Teams conversation, including information about the user, channel, and meeting.
- Teams specific route attributes: Provides attributes for routing messages and events to specific Teams channels or meetings.

## Meeting channel data

With Microsoft.Teams.Apps 2.2.0, `TeamsGetMeetingInfo()` returns
`Microsoft.Teams.Apps.Schema.TeamsChannelDataMeeting` instead of
`Microsoft.Teams.Apps.Clients.Meeting`. This is a source-breaking return-type change.
The helper reads the meeting identifier from channel data and returns `null` when
meeting data is absent. Full REST meeting details require a separate meeting API call.

```csharp
using Microsoft.Agents.Extensions.MSTeams;

var meeting = turnContext.Activity.TeamsGetMeetingInfo();
if (meeting != null)
{
    await turnContext.SendActivityAsync(
        $"Meeting ID: {meeting.Id}",
        cancellationToken: cancellationToken);
}
```

## Purview sensitivity labels

Use the shared Core `SensitivityUsageInfo.AtId` for a Purview label's actual GUID.
Teams resolves the label from this identifier; `Name` and `Description` are display
fallbacks, not substitutes for the identifier. For message-level labels, `Position`
can carry the priority used by Teams to choose the most restrictive label. Core
keeps this metadata generic and does not resolve or validate Teams label IDs.

An ordinary Core message can carry the label inside its root message entity:

```csharp
using Microsoft.Agents.Core.Models;

var label = new SensitivityUsageInfo
{
    AtId = purviewLabelId,
    Name = "Confidential",
    Position = 1
};
var reply = new Activity
{
    Type = ActivityTypes.Message,
    Text = "Labeled response.",
    Entities = [new AIEntity { UsageInfo = label }]
};
await turnContext.SendActivityAsync(reply, cancellationToken);
```

If a message already has an `AIEntity` for citations or AI labeling, set its
`UsageInfo` rather than adding a second root. A citation-specific label belongs in
`ClientCitationAppearance.UsageInfo`.

For streamed or buffered output, use the existing streaming API:

```csharp
turnContext.StreamingResponse.SensitivityLabel = label;
turnContext.StreamingResponse.EnableGeneratedByAILabel = false;
turnContext.StreamingResponse.QueueTextChunk("Labeled response.");
await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
```

The final message includes the label even without citations or an AI-generated
marker. Setting `SensitivityLabel` alone does not claim that content is AI-generated.
No new ApiClient wrapper or upstream activity model is required.

## Pre-authorized incoming attachments

Register the existing downloader once before building the host:

```csharp
using Microsoft.Agents.Hosting.AspNetCore;

builder.AddAgentM365AttachmentDownloader();
```

In an agent route, consume the existing downloaded-input collection:

```csharp
foreach (var file in turnState.Temp.InputFiles)
{
    await turnContext.SendActivityAsync(
        $"Received {file.Filename} ({file.Content.ToMemory().Length} bytes).",
        cancellationToken: cancellationToken);
}
```

For Teams and M365 Copilot attachments, an HTTPS `content.downloadUrl` is already
authorized by its URL. The downloader does not resolve a token provider or add a
bearer for that request, including when `ContentUrl` is absent. Invalid signed URLs
and unsuccessful HTTP responses are skipped without falling back to `ContentUrl`
or another identity.

`UseAnonymous` is not required for signed URLs; it still controls authentication
for direct `ContentUrl` downloads. Those authenticated downloads acquire one token
lazily per batch. Existing optional host/DNS and redirect validation is unchanged.
Keep custom HTTP handlers for this downloader credential-free; it selects request
authentication itself.
