# Microsoft.Agents.Builder

## About

The primary package for building agents with the Microsoft 365 Agents SDK. Provides the `AgentApplication` routing framework, middleware pipeline, and turn context model for handling conversational activities across channels and platforms.

## Main Types

- `AgentApplication`: Base class for agents with route-based activity handling and middleware support
- `IAgent`: Core agent interface implemented by all agents
- `ITurnContext`: Provides access to the current activity, channel, and services for a given turn
- `ITurnState`: Per-turn state container

## Agentic-user shared files

The built-in `M365AttachmentDownloader` resolves uploaded-file attachments whose
metadata has no `downloadUrl` through Graph. This is shared Core/Builder
functionality for Teams and M365 Copilot; it does not require the MSTeams extension.
With ASP.NET Core hosting, keep the existing opt-in registration:

```csharp
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;

builder.AddAgentM365AttachmentDownloader(new M365AttachmentDownloaderOptions
{
    GraphBaseUrl = "https://graph.microsoft.com/v1.0"
});
```

In an agent route, consume the existing input-file collection:

```csharp
foreach (var file in turnState.Temp.InputFiles)
{
    await turnContext.SendActivityAsync(
        $"Received {file.Filename} ({file.ContentType}).",
        cancellationToken: cancellationToken);
}
```

The Graph route requires an `agenticUser` recipient with a tenant, agent instance,
and agentic-user identifier in a personal, group-chat, or channel conversation.
The selected connection must implement `IAgenticTokenProvider`. Its
`GetAgenticUserTokenAsync` acquires the token using the Graph origin's `/.default`
scope, independently of Bot API credentials. The blueprint needs consented
file/site permissions, and the file must be accessible to the agentic user.
Decoded tokens with no `Files.*` or `Sites.*` permission are skipped before HTTP;
this is a diagnostic preflight, not token validation. Graph authorizes opaque
tokens and remains authoritative for actual access.

`GraphBaseUrl` includes the API version and must use HTTPS without user information,
a query, or a fragment. A sovereign endpoint such as
`https://graph.microsoft.us/v1.0` requests `https://graph.microsoft.us/.default`.
When optional outbound validation is enabled, explicitly allowlist any Graph
or redirected storage hosts not included in the existing default policy.

The original sharing URL is encoded byte-for-byte for Graph's `/shares` endpoint,
not fetched directly. Its name and original `ContentUrl` remain on the returned
`InputFile`; MIME comes from the response, or falls back to
`application/octet-stream`. Graph credentials are cached separately once per
download batch. Redirects use HTTPS only and permanently drop the bearer after
leaving the initial origin; optional host/DNS checks still apply to every hop.

**Behavior change:** uploaded-file metadata without a signed URL no longer uses
a direct `ContentUrl` GET. Malformed or unsupported scopes/actors, anonymous mode,
missing credentials, and denied HTTP responses are skipped with diagnostics.
There is no app-only, Bot API, or browsable-URL fallback. Cancellation propagates.
An explicitly supplied signed URL still takes precedence, and a failed signed
URL never falls back to Graph. Inline images and ordinary non-file direct
downloads retain their existing routes.
