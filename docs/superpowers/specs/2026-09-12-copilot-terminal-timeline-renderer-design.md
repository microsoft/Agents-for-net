# Copilot-Style Terminal Timeline Renderer

## Context

The current `CopilotStudioClient.Terminal` sample uses Terminal.Gui `FrameView`
and `Markdown` controls for its conversation surface. Changing tab navigation,
adding glyphs, and applying an accent scheme does not materially change the
default boxed, mostly monochrome presentation. It therefore does not reproduce
the approved Copilot-style timeline mockup.

This design replaces the Markdown transcript with a purpose-built Terminal.Gui
renderer while preserving the sample's conversation, protocol interpretation,
activity journal, composer, actions, and inspector behavior.

## Goals

- Present the default conversation as a sparse, expressive timeline similar to
  GitHub Copilot CLI.
- Render streaming responses and active thoughts in place without duplicate
  rows.
- Preserve reliable scrolling, keyboard input, resizing, and alternate
  Thoughts and Activities inspectors.
- Adapt semantic colors to the terminal theme instead of forcing a dark
  palette.
- Keep protocol interpretation independent from terminal rendering.

## Non-goals

- Reimplement Terminal.Gui, terminal input handling, or ANSI capability
  detection.
- Change Copilot Studio protocol interpretation or activity correlation.
- Replace the existing activity JSON inspector.
- Add user-selectable themes in this iteration.
- Display every protocol activity in the conversation timeline.

## Architecture

### `TerminalChatState`

`TerminalChatState` remains the ordered, keyed source of conversation entries.
It continues to apply `ChatChange` upserts and removals and to own active links
and suggested actions. Rendering-specific Markdown generation is removed.

The state exposes immutable ordered entries to the renderer. Existing keys
remain the identity used for streaming replacement, transient removal, and
action-group tracking.

### `TerminalTimelineView`

A new focused Terminal.Gui view owns timeline layout, drawing, and scrolling.
It does not interpret Activities or mutate conversation state.

Its responsibilities are:

- Accept an immutable ordered snapshot of `ChatEntry` values.
- Convert each entry into semantic header and wrapped body lines.
- Draw only the visible viewport with per-kind terminal attributes.
- Recalculate wrapping and content height when its width changes.
- Preserve the current scroll offset during updates unless the view was
  following the latest entry, in which case it follows the new bottom.
- Keep update-by-key entries spatially stable during streaming.
- Sanitize control characters before measuring or drawing text.

The view uses Terminal.Gui input and drawing primitives so mouse/keyboard
scrolling, focus, alternate-screen restoration, and terminal capability
handling remain delegated to Terminal.Gui.

### Application composition

`TerminalChatApplication` replaces the conversation `Markdown` control with
`TerminalTimelineView`. The surrounding conversation frame border is removed.

The presenter and interpreter contracts do not change:

1. `TerminalPresenter` receives an Activity.
2. `ActivityInterpreter` emits `ChatChange` values.
3. `TerminalChatApplication` applies changes to its chat and thought states.
4. The application passes the resulting entry snapshots to the appropriate
   timeline views.

The composer, action bar, status reporting, startup lifecycle, and send-task
tracking remain unchanged.

## Visual behavior

### Semantic entries

Timeline entries use blank-line rhythm instead of cards or box borders.

| Kind | Glyph | Treatment |
| --- | --- | --- |
| User | `>` | Accent header and normal body |
| Agent | `●` | Success/accent header and normal body |
| Transient status | `○` | Muted header and body |
| Thought | `◆` | Secondary accent header and muted body |
| Meaningful event | `↗` | Muted compact row |
| Attachment or action | `▣` | Accent compact row |
| Diagnostic | `!` | Warning or error emphasis |

ASCII-compatible fallbacks are used when a terminal cannot render a preferred
glyph. Color attributes derive from the active Terminal.Gui base scheme and
terminal capabilities. The renderer must remain readable in light, dark, and
limited-color terminals.

### Thoughts

An active streamed thought appears inline and updates in place. When its stream
completes, the conversation timeline collapses it to a one-line summary. The
complete thought history remains available in the Thoughts inspector through
`Ctrl+2`.

Collapsed thought summaries must identify the thought and communicate that the
full content is available in the inspector. They must not expose hidden
chain-of-thought content beyond what the service already supplied for display.

### Activities

Only meaningful events appear inline: diagnostics, attachment/link/action
events, and protocol events already promoted by `ActivityInterpreter` into a
visible `ChatEntry`. All inbound, outbound, and diagnostic records continue to
be captured by `ActivityJournal` and remain available under `Ctrl+3`.

### Composer and footer

The composer stays pinned to the bottom and gains a distinct border with a
leading `>` prompt. The status line and shortcut footer use muted and accent
attributes to create hierarchy without competing with the conversation.

The empty state shows a compact product title, connection state, and one-line
usage hint instead of a large blank framed region.

### Navigation

- `Ctrl+1` shows and focuses Conversation.
- `Ctrl+2` shows and focuses Thoughts.
- `Ctrl+3` shows and focuses Activities.
- `Ctrl+4` opens Help.
- Split mode retains Conversation and Thoughts on the left with Activities on
  the right.

## Layout and scrolling

The timeline wraps message bodies to the current content width. Continuation
lines align with the body rather than the glyph. Layout is recomputed after a
resize or entry update.

When the user is at the bottom, new and updated streaming content keeps the
latest line visible. If the user has scrolled upward, updates preserve their
viewport and do not pull them back to the bottom. Switching away from and back
to Conversation preserves its scroll position.

Narrow layouts degrade by reducing indentation and metadata before truncating
content. No width may produce negative dimensions or throw during layout.

## Error handling

- Control characters are escaped or replaced before display.
- Unsupported glyphs use ASCII fallbacks.
- Missing or incomplete entry metadata uses the existing author and entry-kind
  defaults rather than preventing rendering.
- Rendering errors are not swallowed. They follow the existing diagnostic
  path and remain inspectable in Activities.
- Terminal.Gui owns terminal restoration during normal shutdown and startup
  failure.

## Testing

Tests will cover:

- Semantic glyph and attribute selection for each entry kind.
- Body wrapping and indentation at normal and narrow widths.
- Re-layout after resize.
- In-place replacement of streaming responses.
- Bottom-follow behavior and preservation of a manually scrolled viewport.
- Active thought display and completed thought collapse.
- Control-character sanitization and low-color/ASCII fallback.
- `Ctrl+1` through `Ctrl+4` navigation.
- Composer, action, link, startup, and activity-inspector regressions.

The terminal sample project must build without warnings, and the complete
`Microsoft.Agents.CopilotStudio.Terminal.Tests` suite must pass.

## Acceptance criteria

The default running experience visibly matches the approved option A:

- no tab strip or large Conversation frame;
- a visually hierarchical, glyph-led conversation timeline;
- distinct semantic treatment for user, agent, thought, event, attachment, and
  diagnostic entries;
- active streaming content updates in place;
- a clearly separated composer and subdued footer;
- full Thoughts and Activities inspectors remain keyboard-accessible;
- behavior remains usable across terminal themes, resize events, and
  limited-color environments.
