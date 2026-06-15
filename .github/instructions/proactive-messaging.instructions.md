---
applyTo:
  - "**/Proactive*"
  - "**/ContinueConversation*"
  - "**/HttpProactive*"
  - "**/CreateConversationOptions*"
  - "**/ConversationBuilder*"
  - "**/ConversationReferenceBuilder*"
---

# Proactive Messaging Context

When working on proactive messaging, conversation continuation, or proactive HTTP endpoints, read `docs/proactive-sequence-diagram.md` for the full mermaid sequence diagrams covering all flows.

## Key Design Points

- **Conversation storage is required** for HTTP-triggered proactive flows. `Proactive.StoreConversationAsync` persists `Conversation` (Claims + ConversationReference) to `IStorage` under key `proactive/conversations/{conversationId}`.
- **Two invocation styles**: HTTP endpoints (via `MapAgentProactiveEndpoints`) for external triggers, and in-code `Proactive.ContinueConversationAsync` / `Proactive.SendActivityAsync` for agent-initiated scenarios.
- **SendActivity** is fire-and-send — it creates a minimal turn to deliver a single activity. **ContinueConversation** creates a full turn with TurnState, middleware, and optional token handling.
- **ProcessProactiveAsync** on the adapter creates the full turn pipeline (state, middleware). This differs from the simpler `ContinueConversationAsync` on the adapter which only provides a raw TurnContext callback.
- **Exception handling** — Exceptions inside proactive callbacks are captured via `ExceptionDispatchInfo` and re-thrown after the adapter call completes. Without this, exceptions in the background pipeline would be silently lost.
- **Token handling** — `[ContinueConversation(autoSignInHandlers: "me")]` attribute triggers automatic token acquisition during proactive turns. If `ProactiveOptions.FailOnUnsignedInConnections` is true (default), `UserNotSignedIn` is thrown when tokens cannot be acquired.
- **HTTP query parameters** are passed through as `Activity.Value` (dictionary) with `ValueType = "application/vnd.microsoft.activity.continueconversation+json"` on the continuation Event activity.
- **CreateConversation** creates a new conversation on the channel (e.g., 1:1 in Teams) and optionally stores it and/or continues into it.

## Endpoint Mapping

`MapAgentProactiveEndpoints<TAgent>()` discovers `[ContinueConversation]` attributes on the agent and maps:
- `/proactive/sendactivity/{conversationId}` — send to stored conversation
- `/proactive/sendactivity` — send with full Conversation in body
- `/proactive/continue[/{key}]/{conversationId}` — continue stored conversation
- `/proactive/continue[/{key}]` — continue with Conversation in body
- `/proactive/create[/{key}]` — create new conversation

## Related Source Files

| Component | Path |
|-----------|------|
| Proactive | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/Proactive.cs` |
| Conversation | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/Conversation.cs` |
| ContinueConversationAttribute | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ContinueConversationAttribute.cs` |
| ContinueConversationRoute | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ContinueConversationRoute.cs` |
| HttpProactive | `src/libraries/Hosting/AspNetCore/HttpProactive.cs` |
| AgentEndpointExtensions | `src/libraries/Hosting/AspNetCore/AgentEndpointExtensions.cs` |
| ProactiveOptions | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ProactiveOptions.cs` |
| Sequence Diagram | `docs/proactive-sequence-diagram.md` |
