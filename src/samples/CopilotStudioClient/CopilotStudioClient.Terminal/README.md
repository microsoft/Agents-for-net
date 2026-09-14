# Copilot Studio Terminal Client

`CopilotStudioClient.Terminal` is an interactive terminal sample for Microsoft Copilot Studio conversations. It renders a borderless, glyph-led conversation timeline in the terminal, uses adaptive colors to separate message types and diagnostics, and keeps persistent navigation available while also journaling every inbound and outbound `Activity` for inspection. Protocol-interpretation diagnostics appear inline in Chat, while the source `Activity` remains inspectable in Activities.

## What it shows

- **Chat**: messages, streaming status, streaming responses, suggested actions, attachments, and adaptive-card `Action.OpenUrl` links.
- **Thoughts**: active thought streams appear inline as they arrive, and completed thought chains stay available as a session summary.
- **Activities**: a full chronological activity inspector plus formatted SDK `Activity` JSON for the selected record.
- **Help**: in-app shortcuts and navigation help.

The activity inspector shows the complete `Activity` model after the SDK has deserialized it and `ProtocolJsonSerializer` has reformatted it with indentation. It does **not** capture raw SSE frames or byte-for-byte HTTP transport payloads.

## Prerequisites

You need all of the following before running the sample:

1. A published agent in **Microsoft Copilot Studio**.
2. The agent metadata needed to connect to it:
   - `DirectConnectUrl`, or
   - both `EnvironmentId` and `SchemaName`.
3. An **Entra ID** app registration for one of these authentication modes:
   - **Interactive user sign-in** with the delegated `CopilotStudio.Copilots.Invoke` permission.
   - **Service principal** with the application `CopilotStudio.Copilots.Invoke` permission.
4. The **Power Platform API** available in your tenant's app-permissions list. If it does not appear under **APIs my organization uses**, follow the tenant setup guidance in [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions).

## Copilot Studio metadata

In Copilot Studio:

1. Open your agent.
2. Publish it.
3. Go to **Settings** > **Advanced** > **Metadata**.
4. Copy the values you plan to use:
   - `Direct connect URL` if your environment exposes one, or
   - `Schema name` and `Environment ID`.

The sample accepts either `DirectConnectUrl` **or** the `EnvironmentId` + `SchemaName` pair.

## Interactive user sign-in setup

Create or reuse a **public client/native** Entra app registration:

1. Open the Azure portal and go to **Entra ID**.
2. Create a new **App registration**.
3. Use these settings:
   - **Supported account types**: `Accounts in this organization directory only`
   - **Platform**: `Public client/native (mobile & desktop)`
   - **Redirect URI**: `http://localhost`
4. In the app registration, record:
   - `Application (client) ID`
   - `Directory (tenant) ID`
5. Open **API permissions** and add:
   - **Power Platform API**
   - **Delegated permissions**
   - `CopilotStudio` > `CopilotStudio.Copilots.Invoke`
6. Optionally grant admin consent.
7. In **Authentication**, enable **mobile and desktop flows**.

## Service-principal setup

> [!WARNING]
> Service-principal sign-in is not generally supported by the current Copilot Studio client sample surface. Use it only when your tenant and agent configuration allow it.

> [!IMPORTANT]
> For service-principal sign-in, the Copilot Studio agent must allow **anonymous user authentication**.

Create or reuse a confidential-client Entra app registration:

1. Open the Azure portal and go to **Entra ID**.
2. Create a new **App registration**.
3. Use **Accounts in this organization directory only**.
4. Record:
   - `Application (client) ID`
   - `Directory (tenant) ID`
5. Open **API permissions** and add:
   - **Power Platform API**
   - **Application permissions**
   - `CopilotStudio` > `CopilotStudio.Copilots.Invoke`
6. Optionally grant admin consent.
7. Create a client secret and copy it for the sample configuration.

## Configuration

Edit `appsettings.json` in this sample directory and fill in `CopilotStudioClientSettings`.

```json
{
  "CopilotStudioClientSettings": {
    "DirectConnectUrl": "",
    "EnvironmentId": "",
    "SchemaName": "",
    "TenantId": "",
    "UseS2SConnection": false,
    "AppClientId": "",
    "AppClientSecret": ""
  }
}
```

### Required values

- `AppClientId`: always required.
- `TenantId`: always required.
- `DirectConnectUrl`: required unless both `EnvironmentId` and `SchemaName` are supplied.
- `EnvironmentId` and `SchemaName`: required together when `DirectConnectUrl` is blank.
- `UseS2SConnection`: set to `true` for service-principal authentication.
- `AppClientSecret`: required only when `UseS2SConnection` is `true`.

### Interactive configuration

Use the public-client/native app registration values and keep:

```json
"UseS2SConnection": false
```

`AppClientSecret` can stay blank in this mode.

### Service-principal configuration

Use the confidential-client app registration values and set:

```json
"UseS2SConnection": true,
"AppClientSecret": "<client-secret>"
```

## Run

From this sample directory:

```powershell
dotnet run
dotnet run -- --layout split
dotnet run -- --help
```

From the repository root:

```powershell
dotnet run --project src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/CopilotStudioClient.Terminal.csproj
dotnet run --project src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/CopilotStudioClient.Terminal.csproj -- --layout split
```

## Layouts

### Default layout

The default layout shows the borderless **Chat** timeline below a persistent navigation row. Full-size inspector surfaces remain hidden until selected:

1. **Chat**
2. **Thoughts**
3. **Activities**
4. **Help**

`--layout tabs` remains accepted for command-line compatibility and selects this same borderless surface mode; it does not render a tab strip.

### Split layout

`--layout split` keeps **Chat** or **Thoughts** in a borderless conversation pane on the left and the responsive **Activities** inspector on the right. The same navigation row, conversation state, activity journal, shortcuts, and commands are used as the default view. At narrow widths, the Activities list stacks above its JSON inspector; at wider widths, the list sits beside the JSON inspector.

## Keyboard shortcuts

- `F1`: show **Chat**
- `F2`: show **Thoughts**
- `F3`: show **Activities**
- `F4`: show **Help**
- `Esc`: return to **Chat**
- `Ctrl+C`: copy the focused link or selected activity JSON
- `Ctrl+Q`: cancel active work and quit
- `Enter`: send the composer text or activate the focused link/action

`Tab` / `Shift+Tab` move focus within the active surface. `Up` / `Down`, `PageUp` / `PageDown`, and the mouse wheel scroll the focused timeline or inspector.

## Rendering and terminal compatibility

The shell renders without stock terminal chrome. A persistent navigation row stays visible above the active borderless surface so the current surface and shortcut keys remain discoverable in default and split layouts.

The shell renders Markdown only for conversational User, Agent, and Thought entries, including headings, bold, italic, inline code, ordered and unordered lists, and `http`/`https` links. Diagnostic entries and non-conversational activity details remain literal so protocol payloads and diagnostics are not reformatted as Markdown.

The palette adapts to dark and light terminal backgrounds when true color is available, and falls back to readable limited-color roles when color detection or true color is unavailable. Terminals that do not report UTF-8 output use ASCII timeline glyphs and ASCII status separators instead of Unicode symbols.

The composer is disabled until startup completes, disabled again while a request is active, and restored after completion, cancellation, or failure. Sending an empty message never calls Copilot Studio; the app shows an in-terminal informational status instead.

## Streaming semantics

The chat transcript follows the same stream interpretation described in the design:

- `informative` updates replace the transient status line.
- `streaming` typing updates append text chunks to the current transient agent response.
- Streaming `thought` text is accumulated independently in the **Thoughts** view.
- `final` activities finalize the visible response and clear the transient status.
- Streams correlate by `streamInfo.streamId`.
- A start activity can omit `streamInfo.streamId`; in that case the sample uses the activity `id` as the provisional stream identifier.
- A final activity can omit `streamSequence` and still complete the stream when the `streamId` and `streamType: final` metadata are present.

Malformed or ambiguous stream metadata never overwrites another visible response. The source activity still appears in **Activities**, and the chat transcript adds a diagnostic entry when needed.

## Thoughts, suggested actions, and adaptive cards

- `thought` and `thoughts` entities are shown in **Thoughts**, not duplicated into Chat.
- **F2 Thoughts** shows agent reasoning and structured tool-call lifecycle blocks. Tool calls update in place from Running to Completed and show the visible parameter snapshot plus elapsed time. Hidden parameters and raw tool results are not shown there; use Activities when raw protocol inspection is required.
- Streaming thought deltas update one entry in place. `chainOfThoughtId` separates concurrent chains when present; otherwise the response stream identifies the chain.
- Completed chains remain in chronological history for the life of the process.
- Unknown entity types are left in the JSON inspector instead of being duplicated into Chat.
- Suggested actions appear in the shared Chat action bar for the active message.
  - Actions with a non-empty `value` populate the composer and send immediately.
  - Actions without a value populate the composer without sending.
- Adaptive-card attachments stay visible even when they contain no `Action.OpenUrl` actions.
- For `application/vnd.microsoft.card.adaptive`, the sample extracts every nested `Action.OpenUrl` link whose `url` is an absolute `http` or `https` URI.
- Malformed adaptive-card JSON creates a diagnostic entry without hiding the attachment.

## Link safety

Received links are never opened automatically.

- Only explicit user activation can open a link.
- Only `http` and `https` links are eligible.
- The sample asks for confirmation before launching a browser.
- `Ctrl+C` can copy the focused link without opening it.

## Activity inspector

Every inbound, outbound, and local diagnostic record is added to the in-memory activity journal for the life of the process. Selecting a record in **Activities** shows:

- the record sequence number in the list,
- its direction and summary,
- and the complete formatted SDK JSON snapshot when an `Activity` payload is available.

Diagnostic-only journal entries intentionally show explanatory text instead of activity JSON.

## Tests

Run the targeted sample test project from the repository root:

```powershell
dotnet test src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj
```

Those tests cover option parsing, presenter/application behavior, activity journaling, streaming interpretation, thought rendering, adaptive-card link extraction including malformed and action-free cards, and the `--help` startup path without requiring Copilot Studio credentials.
