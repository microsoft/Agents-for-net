# ConsoleProactive

This sample sends a proactive message to an **existing conversation** from a plain console
application — **without** a `CloudAdapter` and **without** an `AgentApplication`. It builds the
outbound `Activity` by hand and posts it using a `RestConnectorClient` directly.

It demonstrates:

1. Using `ConversationReferenceBuilder` (from `Microsoft.Agents.Builder.App.Proactive`) to construct
   a `ConversationReference`. The `ServiceUrl` is derived from the channel automatically.
2. Creating an `Activity` of type `message`, setting `Activity.Text`, and applying the
   `ConversationReference` with `ApplyConversationReference`.
3. Creating a `RestConnectorClient` directly and calling `SendToConversationAsync` — no adapter.

The only service taken from DI is `IHttpClientFactory`, which both `MsalAuth` and
`RestConnectorClient` use to create `HttpClient` instances.

## Usage

All values are passed as command-line arguments (no `appsettings.json`):

```bash
dotnet run --project src/samples/ConsoleProactive/ConsoleProactive.csproj -- \
  [--tenant <TenantId>] <ChannelId> <ClientId> <ClientSecret> <ConversationId> <Text...>
```

| Argument | Description |
| --- | --- |
| `--tenant <TenantId>` | *(Optional)* Tenant ID. If supplied, a single-tenant authority is used; otherwise the multi-tenant `botframework.com` authority is used. |
| `ChannelId` | Channel of the target conversation (for example `msteams`). |
| `ClientId` | Client ID of the Azure Bot / Entra app registration. |
| `ClientSecret` | Client secret of the app registration. |
| `ConversationId` | ID of an existing conversation to send to. |
| `Text...` | The message text (remaining arguments are joined with spaces). |

> The `ConversationId` identifies an **existing** conversation. In a real Agent this is captured
> during a prior turn (for example via `new Conversation(turnContext)` or
> `Proactive.StoreConversationAsync`) and persisted to storage.

## Further reading

To learn more about building Agents, see the [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
