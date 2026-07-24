# Fix invalid Mermaid syntax in sequence diagrams

An automated lint pass (`mmdc` from `@mermaid-js/mermaid-cli`) found **invalid Mermaid** in
one or more `docs/*sequence-diagram.md` files — most likely introduced by the preceding
diagram-audit edit. The exact parser errors are appended to the end of this prompt under a
line `=== MERMAID LINT ERRORS ===` (each error includes the file path and the line/column
where parsing failed).

## Your task

Fix **only** the Mermaid syntax so that every ```mermaid``` block parses and renders cleanly.

## Rules

- You may **only** edit `docs/*sequence-diagram.md` files. Never modify source code, tests,
  build files, or anything under `src/`.
- Make the **smallest** change that makes the Mermaid valid. Do **not** restructure diagrams,
  rename participants, or change the meaning/flow they describe. The goal is to repair syntax,
  not to re-audit the diagrams.
- Preserve the existing prose, tables, participant aliasing, style, and each file's encoding
  (these files may use a UTF-8 BOM — keep it).
- Use the lint errors below to locate each problem, then correct the offending Mermaid
  statements. Common causes: a malformed message/arrow line, an unbalanced
  `alt`/`opt`/`loop`/`par`/`critical`/`end` block, a mismatched `activate`/`deactivate`, or
  an illegal character in a message or note.
- Do not introduce new participants or steps that are not already present in the diagram.

## Verify

Re-read every block you changed and confirm it is valid Mermaid `sequenceDiagram` syntax
before finishing. Do not print a summary — just make the edits.
