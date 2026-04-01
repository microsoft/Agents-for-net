# Microsoft.Agents.Extensions.Teams

`Microsoft.Agents.Extensions.Teams` extends the Microsoft 365 Agents SDK with the full set of
Microsoft Teams extensibility features. It provides fluent route builders and declarative route
attributes for Message Extensions (search queries, link unfurling, action commands, compose
previews), Task Modules, Meetings (start/end, participants join/leave), channel and team lifecycle
events, and a broad set of conversation update events such as file consent, message edits, and read
receipts. Alongside the routing surface, the package includes `TeamsInfo` — a static helper for
querying Teams roster, meeting, and team data. 

---

## Getting Started

Use the `[TeamsExtension]` attribute on a `partial` class to have the source
generator wire up the extension automatically and discover route attribute-decorated methods:

```csharp
[TeamsExtension]
public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [QueryRoute("searchCmd")]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSearchAsync(
        ITurnContext ctx, ITurnState state,
        Microsoft.Teams.Api.MessageExtensions.Query query,
        CancellationToken ct)
        => ResponseTask.WithResult(BuildResults(query));
}
```
The source generator will create a `TeamsAgentExtension` instance exposed as a "Teams" property on the AgentApplication.

---

## Feature Areas

- Message Extensions: (Teams.MessageExtensions) Handles compose-box and command-bar interactions.
- Task Modules: (Teams.TaskModules) Handles modal dialogs launched from messages or message extensions.
- Meetings: (Teams.Meetings) Handles Teams meeting lifecycle events.
- Channels: (Teams.Channels) Handles channel lifecycle events within a Team.
- Teams: (Teams.Teams) Handles Team lifecycle events.
- Additional AgentApplication Routes (on `TeamsAgentExtension` directly)
  - Conversation events
  - File consent
  - Message edits and deletes
  - Read receipts
  - Configuration page fetch and submit
