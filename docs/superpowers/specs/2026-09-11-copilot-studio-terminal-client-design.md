# Copilot Studio Terminal Client Sample Design

## Summary

Add a cross-platform, interactive terminal sample named
`CopilotStudioClient.Terminal`. The sample connects to a Microsoft Copilot
Studio agent, presents the conversation in a Web Chat-style interface, and
provides a protocol inspector for every inbound and outbound activity.

The default interface uses full-screen tabs. A split layout showing chat and
activity inspection together is selectable at launch.

## Goals

- Preserve the existing Copilot Studio client sample's connection,
  configuration, and MSAL authentication patterns.
- Replace punctuation-based activity reporting with an interactive terminal
  interface.
- Render messages, streaming progress, known entities, adaptive-card links,
  suggested actions, and diagnostics in a human-readable chat transcript.
- Retain every inbound and outbound activity for the process lifetime.
- Let users select an activity and inspect its complete, formatted JSON.
- Support Windows, macOS, and Linux interactive terminals.
- Keep protocol interpretation independent from Terminal.Gui so it can be
  tested without a terminal or Copilot Studio credentials.

## Non-goals

- Capturing byte-for-byte SSE or HTTP wire payloads. `CopilotClient` exposes
  deserialized `Activity` instances, so the inspector shows their complete SDK
  representation.
- Rendering adaptive cards graphically.
- Rendering every possible entity type in the Chat tab.
- Persisting or exporting conversations.
- Supporting concurrent user requests in one conversation.
- Connecting automated tests to a live Copilot Studio agent.

## Repository Organization

Create the sample at:

`src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\`

Create its tests at:

`src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\`

Add both projects to `src\Microsoft.Agents.SDK.sln`. Place the sample under the
existing `Samples\CopilotStudioClient` solution folder and the test project
under `Tests`.

The sample targets `net10.0`, matching the existing
`CopilotStudioClient` sample. It references
`Microsoft.Agents.CopilotStudio.Client` as a project reference and uses
Terminal.Gui as its sole terminal UI framework. Add the Terminal.Gui package
version to `Directory.Packages.props` and omit versions from project-level
`PackageReference` elements.

## Architecture

### `ConversationSession`

Owns the conversation ID and the interaction with `CopilotClient`.

- Starts the conversation with `EmitStartConversationEvent = true`.
- Converts submitted text to an outbound message activity.
- Publishes that outbound activity before calling `ExecuteAsync`.
- Publishes every activity yielded by `StartConversationAsync` and
  `ExecuteAsync`.
- Allows only one execution at a time.
- Propagates cancellation and failures to the application controller.

### `ActivityJournal`

Stores immutable, ordered records for the current process.

Each record contains:

- a monotonically increasing sequence number;
- direction: inbound, outbound, or local diagnostic;
- timestamp;
- activity type;
- a compact summary;
- the activity object when applicable;
- indented JSON when applicable;
- diagnostic severity and text when applicable.

The journal is safe for network callbacks and UI reads from different threads.
Records are retained until the process exits. Selection is keyed by journal
sequence number so incoming activities do not change the selected item.

### `ActivityJsonFormatter`

Serializes activities with `ProtocolJsonSerializer`, then formats the result
with `System.Text.Json` indentation. The result represents all data modeled by
the SDK activity after deserialization; it is not described as raw wire JSON.

Serialization failures surface as local diagnostic records rather than
silently producing incomplete JSON.

### `ActivityInterpreter`

Transforms protocol activities into presentation-neutral chat state.

Responsibilities:

- create ordinary user, agent, event, status, thought, attachment, and
  diagnostic entries;
- correlate stream updates and mutate the corresponding logical agent entry;
- render known entities and leave unknown entities in the activity inspector;
- associate suggested actions with their agent message;
- invoke adaptive-card link extraction;
- report malformed or inconsistent stream metadata without applying it to an
  unrelated response.

The interpreter exposes state-change notifications that carry no Terminal.Gui
types.

### `AdaptiveCardLinkExtractor`

Examines adaptive-card attachment content and recursively finds every object
whose `type` is `Action.OpenUrl` and whose `url` is a string.

It supports object, array, `JsonElement`, and string JSON content through a
single normalized JSON traversal. Invalid content returns a structured
extraction failure so the caller can preserve the attachment and show a
diagnostic.

### `TerminalChatApplication`

Owns Terminal.Gui initialization, controls, focus, key bindings, and shutdown.
It observes journal and chat-state changes and dispatches all control updates
onto the Terminal.Gui main loop.

The view contains no stream-correlation or adaptive-card parsing logic. Its
responsibility is to render state and convert user gestures into controller
commands.

### `TerminalOptions`

Parses:

- `--layout tabs`, the default;
- `--layout split`;
- `--help`.

An unsupported layout produces usage text and a nonzero exit code before
authentication or terminal initialization.

## Terminal Experience

### Tab Layout

The default `--layout tabs` mode contains:

- **Chat:** a scrollable conversation transcript, transient connection or
  streaming status, and a single-line composer.
- **Activities:** a chronological activity list on the left and the selected
  activity's formatted JSON on the right.
- **Help:** configuration guidance, layout documentation, and keyboard
  shortcuts.

Activity rows show sequence, direction, type, and a compact summary. Direction
markers distinguish outbound and inbound traffic. Local diagnostics are
visually distinct and have explanatory text rather than activity JSON.

### Split Layout

`--layout split` keeps Chat visible on the left and places the activity list
and selected JSON on the right. It uses the same journal, chat model, commands,
and selection behavior as tab mode.

### Keyboard Interaction

- `Ctrl+1`: focus Chat.
- `Ctrl+2`: focus Activities.
- `Ctrl+3`: focus Help in tab mode.
- `Ctrl+Q`: cancel active work and quit.
- `Tab` and `Shift+Tab`: move focus.
- `Enter`: send the composer contents or activate the focused action.
- `Ctrl+C`: copy the selected URL or activity JSON when clipboard integration
  is available.

Submitting an empty composer does not call Copilot Studio and instead shows an
in-app notice. The composer is disabled while a request is active and restored
after completion or failure.

Received URLs are never launched automatically. Links are exposed as selectable
chat actions so any copy or open operation is explicit and user initiated.

## Activity Presentation

### Messages and Events

Message activities produce chat entries using their text and text format.
Suggested actions are presented beneath the associated message and can populate
or submit the composer.

Typing activities without streaming metadata and event activities appear as
compact status or system entries instead of punctuation. Every activity remains
available in Activities regardless of whether it creates a chat entry.

### Streaming

The Chat tab mirrors Teams streaming semantics:

- an `informative` update replaces the transient agent status;
- a `streaming` update replaces that status and creates or updates one logical
  agent response;
- streaming text is treated as cumulative rather than appended blindly;
- a `final` activity locks the response into the transcript and clears its
  transient state.

Streams are correlated by `streamInfo.streamId`. A start activity can omit that
property; in that case its activity `id` becomes the provisional stream ID.
Subsequent updates identify the stream by setting `streamInfo.streamId` to the
start activity's `id`. If neither value is available, the interpreter may use
the only currently open stream, but it must not merge an update when more than
one stream could match.

Start and continuation activities use increasing `streamSequence` values.
Final activities can legitimately omit `streamSequence`; their
`streamType: final`, `streamId`, and optional `streamResult` close the matching
response.

Sequence regressions on non-final updates, mismatched IDs, missing metadata
required for that stream phase, or otherwise malformed stream information
remain visible in Activities and create a local diagnostic entry. They do not
overwrite another chat response.

### Entities

Known `thoughts` entities produce visually distinct thought entries in Chat.
The renderer uses the entity's available textual fields without assuming one
field is always present.

`streamInfo` is consumed by stream handling and is not duplicated as a generic
entity entry. Unknown entity types remain visible in the formatted activity
JSON but do not clutter Chat.

### Attachments and Adaptive Cards

Attachments produce compact entries containing their content type and content
URL when present. For
`application/vnd.microsoft.card.adaptive`, every valid `Action.OpenUrl` is
shown as a selectable chat link.

Malformed adaptive-card content does not hide the attachment or its parent
activity. It produces a diagnostic and no extracted links.

## Data Flow

1. Startup validates options and verifies that input and output are attached to
   an interactive terminal.
2. Terminal.Gui initializes and displays connection progress.
3. `ConversationSession` starts the Copilot Studio conversation in cancellable
   asynchronous work.
4. Each inbound activity is serialized into an immutable journal record before
   interpretation.
5. `ActivityInterpreter` updates presentation-neutral chat state.
6. Journal and chat changes are marshaled onto the terminal UI loop.
7. When the user submits text, the composer is disabled and an outbound
   activity is created and journaled.
8. `ConversationSession.ExecuteAsync` yields inbound activities through the
   same journal and interpretation pipeline.
9. Completion or failure restores the composer and updates the status area.
10. Quit cancels active SDK enumeration, stops the UI loop, and restores the
    terminal before the process exits.

## Error Handling

- Configuration and option errors are shown before connection and return a
  nonzero exit code.
- Authentication and startup failures appear in the status area, create local
  diagnostics, and return a nonzero exit code if no usable session was
  established.
- Execution failures create visible diagnostics and restore the composer so
  the user may retry.
- Cancellation is distinct from failure and does not create an error-shaped
  success result.
- Invalid adaptive-card JSON and inconsistent streaming metadata are surfaced
  without dropping the source activity.
- The sample does not use broad catches or silent fallback data. Exceptions are
  handled only at application boundaries where they can be shown and mapped to
  lifecycle behavior.

## Testing

Add unit tests for:

- informative, streaming, and final update behavior;
- cumulative streaming text;
- independent concurrent stream IDs;
- initial updates whose activity `id` becomes the later `streamId`;
- final updates without `streamSequence`;
- sequence regressions, mismatched IDs, and ambiguous malformed updates;
- thoughts presentation and unknown-entity omission;
- nested and multiple adaptive-card open URLs;
- malformed, empty, and action-free adaptive cards;
- inbound, outbound, and diagnostic journal ordering;
- formatted activity JSON;
- tab and split option parsing;
- unsupported options and help output;
- presenter state changes without initializing a real terminal.

Use an `InternalsVisibleTo` declaration for the test assembly rather than making
sample implementation types public.

The UI shell is validated by compiling it and exercising a `--help` startup
smoke test. Automated tests do not require credentials and do not connect to
Copilot Studio.

## Documentation

The sample README documents:

- Copilot Studio and Entra ID prerequisites;
- interactive and service-principal configuration;
- commands for default and split layouts;
- controls and shortcuts;
- streaming interpretation;
- thoughts, unknown entities, adaptive-card links, and suggested actions;
- the distinction between formatted SDK activity JSON and raw transport bytes;
- how to run the targeted tests.

## Acceptance Criteria

- Running without a layout option opens the tab interface.
- Running with `--layout split` opens the split interface.
- A user can authenticate, start a conversation, send text, and receive
  responses.
- Incoming streaming activities update one response according to Teams
  semantics.
- Known thoughts and adaptive-card open URLs are visible in Chat.
- Every inbound and outbound activity appears in chronological order in
  Activities.
- Selecting an activity shows complete, indented SDK activity JSON.
- Network, authentication, stream metadata, and adaptive-card parsing failures
  are visible and do not leave the terminal or composer in a broken state.
- The terminal is restored on normal quit, cancellation, and startup failure.
- Targeted tests pass without Copilot Studio credentials.
