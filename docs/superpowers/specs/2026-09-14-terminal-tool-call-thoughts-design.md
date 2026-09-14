# Terminal Tool-Call Thoughts Design

## Purpose

Show upstream `toolCall` entities as structured informational blocks on the
terminal client's Thoughts surface. A block begins in a running state and is
updated in place when the matching completion entity arrives.

This feature is limited to tool-call lifecycle visibility. It does not display
raw tool results, invoke tools, or change the protocol sent to Copilot Studio.

## Source Payload

Tool lifecycle information arrives on inbound activities as entities whose
`type` is `toolCall`. The observed start and completion payloads share a stable
`toolCallId` and contain:

- `toolName`
- `toolDisplayName`
- `toolCategory`
- `status`
- `filledParameters`
- `unfilledParameters`
- `hiddenFilledParameters`
- `hiddenUnfilledParameters`
- `durationMs` on completion
- `result` on completion

Property names, entity type, and known status values are interpreted
case-insensitively. Unknown properties are ignored.

## Lifecycle and Correlation

Each valid tool entity produces an upsert with the stable key
`tool:{toolCallId}`. The existing keyed `ChatChange` and `TerminalChatState`
pipeline therefore preserves the block's original position while replacing
its content as the lifecycle advances.

- A `started` entity creates or updates a Running block.
- A `completed` entity updates the same block to Completed.
- Completion is self-contained and renders even when its start was not seen.
- A started call with no completion remains Running. The client does not invent
  a timeout or duration.
- Other nonblank statuses remain visible using their supplied status rather
  than being mislabeled.
- A missing or blank `toolCallId` produces a warning diagnostic because the
  entity cannot be correlated.
- When a completion supplies parameters, that completion snapshot replaces
  the started snapshot. It is the authoritative final parameter set.

Tool entries are created for both ordinary and streaming activities. Stream
sequence/status processing remains independent and unchanged.

## Data Model

Add `ChatEntryKind.ToolCall` and an optional structured `ToolCallDetails`
payload on `ChatEntry`. The payload contains:

- Correlation ID
- Tool name
- Optional display name
- Optional category
- Status
- Visible filled parameters as ordered name/value pairs
- Visible unfilled parameter names
- Optional nonnegative duration in milliseconds

Parameter values remain `JsonElement` values until timeline projection so
their types are preserved. Strings render directly. Numbers, booleans,
objects, arrays, and null render as compact JSON.

The structured model keeps protocol parsing separate from presentation,
supports deterministic lifecycle upserts, and avoids constructing Markdown
from untrusted parameter values.

## Privacy and Data Minimization

The terminal never copies these fields into `ToolCallDetails`, entry text,
diagnostics, or rendered lines:

- `hiddenFilledParameters`
- `hiddenUnfilledParameters`
- `result`

Visible parameter values are rendered literally through the existing
control-character sanitation and terminal-cell wrapping pipeline. No raw tool
result is copied into the Thoughts model, rendered block, or diagnostics. The
existing Activities inspector remains an intentional raw-protocol view and is
outside this feature's data-minimization boundary.

## Surface Behavior

Tool-call entries appear only on the F2 Thoughts surface. The main Chat surface
continues showing its existing compact streaming status text and does not add
tool blocks.

The Thoughts filter includes both `ChatEntryKind.Thought` and
`ChatEntryKind.ToolCall`. A tool call is never collapsed as a completed chain
of thought.

## Selected Presentation

The approved visual treatment is the timeline narrative:

```text
◆ current_weather
  Connector · Get current weather

  ✓ Completed in 2.97 s

  Called with
    Location = Seattle, WA, USA
    units = I

  No parameters were left unfilled.
```

While running, the same block uses:

```text
◆ current_weather
  Connector · Get current weather

  ◌ Running

  Calling with
    Location = Seattle, WA, USA
    units = I

  Waiting for
    parameterName
```

Presentation rules:

- The tool name uses the adaptive high-contrast accent.
- Category, display name, labels, and explanatory sentences use subdued
  semantic colors.
- Completed status uses the success semantic color.
- Running and unknown statuses remain informational.
- A missing display name or category is omitted without a placeholder.
- Empty filled parameters produce `Called with no parameters.` or
  `Calling with no parameters.`
- Empty unfilled parameters produce
  `No parameters were left unfilled.` for completed calls and
  `No parameters are waiting to be filled.` for running calls.
- The Unicode timeline uses the existing Thought glyph. ASCII mode uses the
  corresponding existing Thought fallback glyph.

## Duration Formatting

The completion entity's `durationMs` is authoritative. The client does not
measure elapsed time locally.

- Below 1,000 ms: integer milliseconds, for example `842 ms`.
- From 1,000 ms through 59,999 ms: compact seconds with at most two fractional
  digits and no trailing zeroes, for example `2.97 s` or `12.4 s`.
- At 60,000 ms or above: whole minutes plus remaining whole seconds, for
  example `2m 13s`. A zero-second remainder is omitted, for example `2m`.
- Missing, nonnumeric, or negative duration is omitted.

## Rendering

`TerminalTimelineLayout` adds a dedicated structured projection path for
`ChatEntryKind.ToolCall`; it does not pass tool data through Markdown parsing.
The path emits semantic spans and blocks, then reuses the existing terminal
cell-width wrapping, grapheme handling, control escaping, and narrow viewport
behavior.

Parameter names retain payload order. Objects and arrays serialize as compact
JSON. Embedded newlines and control characters are sanitized by the same
literal rendering rules used for other non-Markdown protocol data.

## Error Handling

- Missing `toolCallId`: warning diagnostic with no hidden fields or result.
- Missing `toolName`: render the display name when available; otherwise render
  `tool`.
- Invalid filled collection shape: preserve it as a visible fallback parameter
  named `parameters` whose value is compact JSON.
- Invalid unfilled collection shape: preserve its compact JSON as one visible
  waiting/unfilled item rather than silently dropping it.
- Unexpected status: render the supplied status as informational.
- Duplicate lifecycle message: deterministic upsert of the same key.

Errors in one tool entity do not prevent other entities, status text,
attachments, or stream updates in the same activity from being interpreted.

## Validation

Automated tests cover:

- Representative started and completed payloads matching the supplied JSON.
- Stable key correlation and in-place Running-to-Completed replacement.
- Completion without a previously observed start.
- Final parameter snapshot replacing the initial snapshot.
- Visible filled and unfilled parameter rendering.
- Scalar, null, object, and array values.
- Hidden parameter and raw result exclusion at model and rendered-line levels.
- Missing IDs and malformed parameter collections.
- Millisecond, second, and minute duration boundaries.
- Inclusion on Thoughts and exclusion from Chat.
- Ordinary and streaming activities.
- Narrow-width wrapping, control sanitation, and Unicode/ASCII glyphs.
- Existing stream sequencing, ordinary thoughts, and status behavior.
