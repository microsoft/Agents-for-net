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
