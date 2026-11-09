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

## Extended markdown (GA)

Teams extended markdown is generally available with teams.net 2.2.0. Use the
`extendedmarkdown` wire value on the existing Core activity; no Teams activity
conversion or new send helper is needed:

```csharp
using Microsoft.Agents.Core.Models;

var reply = new Activity
{
    Type = ActivityTypes.Message,
    TextFormat = "extendedmarkdown",
    Text = "## Result\n\n**Status:** Complete."
};
await turnContext.SendActivityAsync(reply, cancellationToken);
```

Choose a format supported by the receiving client; this is Teams formatting
guidance, not a change to other channels' rendering or to the default text format.
GA extended markdown does **not** make HTML widgets GA. Widget composition,
runtime handshakes, and call-tool routing remain experimental and are not enabled
by setting `TextFormat`.

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

## Pasted inline images

The shared `M365AttachmentDownloader` handles pasted images through the existing
`builder.AddAgentM365AttachmentDownloader()` registration. This is Core/Builder
functionality: it does not require the MSTeams package or `[TeamsExtension]`.
No second downloader registration or Files facade is needed:

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

public class ImageAgent : AgentApplication
{
    public ImageAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, async (context, state, cancellationToken) =>
        {
            foreach (var file in state.Temp.InputFiles)
            {
                await context.SendActivityAsync(
                    $"Received {file.Filename} ({file.ContentType}).",
                    cancellationToken: cancellationToken);
            }
        }, rank: RouteRank.Last);
    }
}
```

An inline image is an `image/*` attachment whose URL path ends in
`/v3/attachments/{id}/views/original`. Its HTTPS URL must be under the inbound
activity's service URL, including its origin and path prefix. This is checked
before resolving credentials, even when optional outbound host validation is
disabled. The downloader reuses the turn's connector bearer rather than selecting
an app-only or Graph identity. Custom connector REST transports must expose that
bearer in their configured client's default Authorization header; otherwise the
image is skipped with a warning. `UseAnonymous` still avoids acquiring credentials.

Redirects use HTTPS only. Authorization is retained on the initial origin and
dropped permanently after leaving it; existing optional host/DNS checks still
apply to every hop. This path uses the shared credential-free manual-redirect
transport, not an automatically redirecting client supplied by a custom handler.
HTTP failures are skipped with status diagnostics.

Downloaded PNG, JPEG, GIF, and WebP signatures determine `ContentType` and the
generated `image-{id}` filename extension. A pasted GIF that arrives as PNG bytes
is therefore returned as PNG, without converting the bytes. HTML image-format
hints, or the attachment's image MIME type when no hint exists, are fallbacks for
unrecognized bytes. Unsupported formats get no invented extension and use
`application/octet-stream`. The companion HTML is not returned as an input file.
Uploaded files retain their existing names and signed-download behavior.

Automatic downloads remain opt-in. The Teams extension does not configure,
wrap, or replace attachment downloaders.

## Agentic-user shared files

The same shared `M365AttachmentDownloader` resolves uploaded-file metadata with
no signed `downloadUrl` through Graph for eligible agentic users. Configure its
`GraphBaseUrl` when needed and consume `turnState.Temp.InputFiles` as above; no
MSTeams activation is required. See the [shared downloader guide](../../Builder/Microsoft.Agents.Builder/README.md#agentic-user-shared-files)
for Graph permissions, supported scopes, sovereign-host configuration, and the
no-fallback policy. Unsigned uploaded-file metadata is no longer fetched directly
from its browsable `ContentUrl`.
