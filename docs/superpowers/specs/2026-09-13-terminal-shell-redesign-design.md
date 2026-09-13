# Copilot Studio Terminal Shell Redesign

## Context

The current terminal client does not match the approved Copilot-style mockup in
Windows Terminal:

- the root `Window`, composer `FrameView`, and `StatusBar` render prominent
  line-drawing chrome;
- the default Terminal.Gui semantic scheme maps most timeline roles to similar
  white and gray attributes;
- Markdown markers such as `**` are displayed literally;
- the navigation bar is visually heavy and primarily useful as a mouse target;
- navigating to Activities or Help hides the only visible navigation;
- `Ctrl+1` through `Ctrl+4` are not portable terminal inputs. Windows Terminal
  and console hosts may encode them as control characters, Escape, NUL, or no
  distinguishable sequence.

This design keeps Terminal.Gui for terminal lifecycle, input editing, focus,
resizing, and drawing, but replaces the stock visual shell and non-portable
navigation model.

## Goals

- Match the screenshot-informed shell approved in the visual companion.
- Keep navigation visible in every view.
- Make keyboard navigation reliable in Windows Terminal with Command Prompt.
- Render common Markdown as styled terminal text rather than literal syntax.
- Provide meaningful semantic color and hierarchy in dark, light, and
  limited-color terminals.
- Preserve live streaming, action links, activity inspection, Thoughts, Help,
  split mode, resizing, scrolling, and local configuration.

## Non-goals

- Replace Terminal.Gui.
- Rebuild terminal input editing or terminal restoration with raw ANSI.
- Implement the complete CommonMark specification.
- Render images, HTML, Markdown tables, or arbitrary embedded content.
- Change Copilot Studio protocol interpretation or activity correlation.

## Root shell

The application uses a borderless Terminal.Gui top-level instead of `Window`.
The root layout contains three persistent regions:

1. a one-row navigation header;
2. a content region that switches among Chat, Thoughts, Activities, and Help;
3. a footer region owned by Chat when Chat is selected.

The navigation header is never part of a switchable content surface. Changing
views therefore cannot hide the path back to Chat.

The default and split layouts share the same root shell. Split mode changes
only the content region: Conversation and Thoughts occupy the left side while
Activities occupies the right. The persistent navigation remains above both.

## Navigation

Navigation uses keys that terminal hosts report distinctly:

| Key | Action |
| --- | --- |
| `F1` | Show and focus Chat |
| `F2` | Show and focus Thoughts |
| `F3` | Show and focus Activities |
| `F4` | Show and focus Help |
| `Esc` | Return from Thoughts, Activities, or Help to Chat |
| `Ctrl+C` | Copy the focused link or selected activity JSON |
| `Ctrl+Q` | Quit |

Bindings are registered at the application or root top-level key-binding layer.
They do not depend on `KeyDown` bubbling from the focused composer,
`TerminalTimelineView`, `ListView`, or `TextView`.

The navigation header displays `F1` through `F4` and active-view state. It is
not implemented with `StatusBar` or `Shortcut` controls and is not presented as
a row of clickable buttons.

After navigation:

- Chat focuses the enabled composer, otherwise the conversation timeline.
- Thoughts focuses the thought timeline.
- Activities focuses the activity list.
- Help focuses the Help content.
- `Esc` returning to Chat follows the Chat focus rule above.

## Visual system

### Chrome

The default shell has no outer line-drawing border. Chat has no surrounding
frame. Thoughts, Activities, and Help also use the same borderless content
region. Separators are used only where they communicate structure.

The composer is visually separated with a compact input border. Unicode-capable
terminals use rounded box-drawing corners; non-Unicode output uses an ASCII
border. The footer is a single subdued text row, not a `StatusBar`.

### Adaptive palette

The shell defines explicit semantic attributes rather than relying on the
default Terminal.Gui roles to produce visual distinction:

| Semantic role | Dark terminal | Light terminal |
| --- | --- | --- |
| Primary text | light neutral | dark neutral |
| Muted text | medium gray | medium gray |
| User / prompt / link | GitHub blue | accessible dark blue |
| Agent / connected | GitHub green | accessible dark green |
| Thought | GitHub purple | accessible dark purple |
| Active navigation | GitHub coral | accessible dark coral |
| Warning | GitHub yellow | accessible ochre |
| Error | GitHub red | accessible dark red |
| Code background | elevated dark neutral | elevated light neutral |

Auto mode selects the palette from the detected terminal background luminance.
If the background cannot be detected, it uses terminal-default foreground and
background with 16-color semantic accents. No text may depend on color alone.

The active navigation item has both color and an underline marker. Connection
state has both a glyph/word and color.

## Timeline

The custom timeline remains keyed by `ChatEntry.Key`; streaming upserts replace
the existing visual entry instead of appending duplicates.

Entries use whitespace, indentation, and semantic glyphs rather than boxes:

- user: blue prompt glyph and author;
- agent: green glyph and author;
- active thought: purple left rule and text;
- completed thought: compact purple summary with `F2 for details`;
- meaningful event: muted compact row;
- attachment or action: blue/coral accent;
- diagnostic: warning or error accent.

Author headers and body text are separate styled spans. Agent-provided Markdown
is not used to style author headers.

## Rich-text rendering

A focused terminal rich-text parser supports:

- ATX headings (`#` through `###`);
- bold (`**text**`);
- italic (`*text*` and `_text_`);
- inline code (backticks);
- unordered list markers (`-`, `*`, `+`);
- ordered list markers;
- Markdown links (`[label](https://...)`);
- paragraph and explicit line breaks.

The parser emits semantic spans and block metadata consumed by the timeline
layout. It removes recognized delimiters from visible output. Links preserve
their URL for existing open/copy behavior.

Unsupported or malformed Markdown is rendered as sanitized plain text. Raw HTML
is never interpreted. Control characters are escaped before measurement and
drawing. Wrapping continues to use Terminal.Gui terminal-cell width while
cutting only at grapheme boundaries.

## Composer and footer

The composer consists of:

- a compact input border;
- a blue `>` prompt;
- the existing `TextField`;
- no `FrameView` title or surrounding window chrome.

The footer renders plain muted text:

```text
Enter send · F1–F4 views · Esc back · Ctrl+C copy · Ctrl+Q quit
```

The footer does not accept focus or mouse activation.

## Activities

The Activities view keeps the activity list and formatted JSON inspector but
uses clearer column allocation and semantic selection colors. Its persistent
navigation header remains visible.

Long activity summaries must be truncated or wrapped within the list column;
they must not collapse into narrow one-word columns as shown in the supplied
screenshot. The JSON pane keeps horizontal and vertical scrolling.

## Error handling and fallback

- Unsupported Markdown falls back to sanitized plain text.
- Unsupported Unicode shell glyphs use ASCII equivalents.
- Limited-color terminals use readable 16-color attributes.
- Theme detection failure uses terminal defaults rather than assuming a
  mismatched background.
- Unknown keys continue to the focused control.
- Navigation handlers surface unexpected failures through the existing
  diagnostic/status path; they are not silently ignored.
- Local `appsettings.json` remains uncommitted and unchanged.

## Testing

Tests cover:

- a borderless root with persistent navigation in all views;
- no `Window`, `FrameView`, or `StatusBar` chrome in the default Chat shell;
- `F1` through `F4` navigation from the composer, timeline, activity list, and
  JSON inspector;
- `Esc` return to Chat from every detail view;
- focus transfer after every navigation action;
- Windows-terminal-equivalent input events rather than synthetic
  `Ctrl+number` keys;
- dark, light, and limited-color palette contrast;
- Unicode and ASCII composer borders;
- Markdown headings, emphasis, code, lists, links, malformed input, and raw
  HTML fallback;
- no visible Markdown delimiter leakage for recognized syntax;
- streaming replacement with styled spans;
- narrow and resized timeline/activity layouts;
- split mode;
- existing startup, send, action, link, copy, and shutdown behavior.

The terminal sample must build with zero warnings and errors, and the complete
terminal test project must pass.

## Acceptance criteria

The running application in Windows Terminal with Command Prompt matches the
approved screenshot-informed mockup:

- persistent navigation is visible from Chat, Thoughts, Activities, and Help;
- `F1` through `F4` and `Esc` work without mouse interaction;
- there is no outer ASCII window, cyan command bar, or titled composer frame;
- user, agent, thought, event, link/action, connection, warning, and error
  elements are visually distinct;
- recognized Markdown is rendered rather than printed literally;
- Chat remains readable at normal and narrow terminal sizes;
- Activities retains a usable list width and always exposes a route back;
- streaming, scrolling, inspectors, actions, links, and local configuration
  continue to work.
