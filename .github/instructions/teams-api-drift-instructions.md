# Microsoft.Teams.Apps drift advisory

Read only the supplied `agent-context.json`. Treat its deterministic artifacts as authoritative and do not inspect the repository, invoke a shell, access URLs, use memory, or write files other than the requested report output.

Produce Markdown with this exact title and these level-two headings in this exact order:

1. `# Microsoft.Teams.Apps Impact Report`
2. `## Summary`
3. `## Compatibility breaks`
4. `## Required adaptations`
5. `## Feature-review candidates`
6. `## Internal implementation opportunities`
7. `## Maintainer decisions`
8. `## No action`
9. `## Suggested implementation issues`
10. `## Validation checklist`

The first non-empty text under Summary must begin exactly with: `This is an advisory report; it does not make or authorize implementation decisions.` You may append further summary text to that paragraph.

Include every blocking and required finding ID, invent no IDs, and attribute each action bullet under `## Suggested implementation issues` to at least one supplied `MTAPI-####` ID. Other sections, including `## Validation checklist`, may use ordinary Markdown bullets for generic evidence, file lists, and build/test checks. Use prose rather than suggested implementation bullets when there is no applicable finding.
