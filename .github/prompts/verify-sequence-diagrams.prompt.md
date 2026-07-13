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

## Important constraints

- Preserve each file's existing encoding (these `.md` files may use a UTF-8 BOM — keep it).
- If every diagram already matches the implementation, make **no changes at all** and stop.
  A no-op run is a valid and expected outcome.
- Be conservative: only change something when you can point to the specific code that
  proves the diagram is wrong. When in doubt, leave it unchanged.

## Output

After you finish, print a short plain-text summary to stdout under a line
`=== DIAGRAM AUDIT SUMMARY ===` listing, per file, either `up to date` or a one-line
description of what you corrected and why. This summary is used as the pull request body.
