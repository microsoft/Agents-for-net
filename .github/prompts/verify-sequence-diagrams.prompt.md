# Verify sequence diagrams against the implementation

You are auditing the Mermaid sequence diagrams under `docs/` in this repository to make
sure they still accurately describe the current source code. Your job is to find drift
between each diagram and the code it documents, and to correct the diagram when it is wrong.

## Scope

- Only inspect files matching `docs/*sequence-diagram.md`.
- You may **only** edit those `docs/*sequence-diagram.md` files. Never modify source code,
  tests, build files, or anything under `src/`.
- Treat the source code as the source of truth. When a diagram disagrees with the code,
  the **diagram** is what gets fixed.

## Procedure

For each `docs/*sequence-diagram.md` file:

1. Read the whole document, including the prose, the ```mermaid``` block(s), and any
   "Key Components" / "Participants" tables that list source file locations.
2. Open every source file the document references (the tables list paths like
   `src/libraries/...`). If a referenced participant or method is not obviously in those
   files, search the repository (`grep`/`glob`) to locate the current implementation.
3. Compare the diagram against the code and look specifically for:
   - **Renamed or removed** participants, classes, interfaces, methods, or properties.
   - **Method/call names** in the diagram that no longer exist or have been renamed
     (e.g. a message `A->>B: DoWorkAsync(x)` where `DoWorkAsync` was renamed or deleted).
   - **Call ordering / control flow** that no longer matches the code (alt/else/loop/par
     branches, the sequence of calls, which component calls which).
   - **File paths** in "Key Components"/"Participants" tables that have moved or no longer
     exist.
   - Conditions and return values described in notes (e.g. "returns true/false",
     delivery-mode branches) that contradict the code.
4. If — and only if — you find a real discrepancy, edit the markdown so the diagram and its
   surrounding prose/tables match the current implementation. Preserve the existing style,
   formatting, participant aliasing, and level of detail. Make the smallest change that
   makes the document correct; do not rewrite diagrams that are already accurate, and do not
   make cosmetic-only edits.
5. Keep the Mermaid syntax valid. Do not introduce new participants or steps that are not
   grounded in the code.

## What NOT to change (avoid audit churn)

These diagrams were themselves authored by an AI pass. A second AI pass will always find
*different-but-equally-valid* ways to model the same correct code. Rewriting those is churn,
not a fix, and it re-opens the same issue every week. **Only edit when the code contradicts a
stated fact — never merely because you would have phrased or modeled it differently.**

Specifically, do **not**:

- **Reword** prose, notes, or labels that are already accurate (e.g. renaming a participant
  alias `Class` → `Type`, or a note from "returns true" to "write completes" when both
  describe the same behavior). A synonym is not a discrepancy.
- **Re-model at a different granularity** when the existing abstraction is already correct —
  e.g. splitting one `Assembly` participant into separate `SIA`/`EIA` participants, or
  collapsing two into one. Only change participant structure if the current one asserts a
  call/relationship the code does not have.
- **Swap one valid example for another** (e.g. changing which real converter class is shown in
  a "usage example") unless the class currently shown **no longer exists** or is no longer
  valid for that scenario.
- **Add optional detail** the diagram deliberately omits. A conceptual diagram that abstracts
  away exact method names is not "wrong" for doing so.

Ask yourself: *"Can I point to a line of code that this diagram states is true but is not?"*
If the answer is no, leave it exactly as-is — even if you would have written it differently.

## Canonical terminology (use these exact terms; do not reintroduce the alternatives)

- The SSE transport for `DeliveryModes.Stream` (i.e., `IActivity.DeliveryMode == DeliveryModes.Stream`) is **`DeliveryModes.Stream` SSE**. It is
  **not** "A2A" / "Agent-to-Agent" — that is a separate protocol. Never label the streaming
  delivery path as A2A.

## Domain notes (apply when judging call ordering)

- **`InvokeResponse` timing:** An agent may send an `InvokeResponse` at any point during a
  turn (via an `InvokeResponse` activity), but it is **not returned to the caller until the
  end of the turn**. For `CloudAdapter`, the `InvokeResponse` is delivered in the body of the
  `HttpResponse` after the turn completes — not synchronously at the point the agent produces
  it. A diagram that shows the invoke response being collected mid-turn and returned to the
  caller only at end-of-turn is therefore **correct**; do not "fix" it to show an immediate
  return. Only flag it if the diagram actually contradicts this (e.g. shows the response
  returned to the caller before the turn ends).

## Important constraints

- Preserve each file's existing encoding (these `.md` files may use a UTF-8 BOM — keep it).
- If every diagram already matches the implementation, make **no changes at all** and stop.
  A no-op run is a valid and expected outcome.
- Be conservative: only change something when you can point to the specific code that
  proves the diagram is wrong. When in doubt, leave it unchanged.

## Output

After you finish, print a short plain-text summary to stdout under a line
`=== DIAGRAM AUDIT SUMMARY ===` listing, per file, either `up to date` or a one-line
description of what you corrected and why. The per-file line is included in that file's
issue comment to explain the drift.
