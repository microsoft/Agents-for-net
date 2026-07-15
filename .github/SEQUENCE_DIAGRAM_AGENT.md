# Sequence Diagram Verification Agent

Automated, weekly verification of the Mermaid sequence diagrams under [`docs/`](../docs)
against the actual implementation, powered by the
[GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/use-copilot-cli).

One workflow drives the flow:

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`verify-sequence-diagrams.yml`](workflows/verify-sequence-diagrams.yml) | Weekly schedule (Mondays 08:00 UTC) or manual `workflow_dispatch` | Reads each `docs/*sequence-diagram.md`, follows its "Key Components"/"Participants" tables into `src/`, and checks the diagram against the code (renamed/removed methods, call ordering, control-flow branches, stale file paths). When a diagram has drifted, it corrects the markdown locally, validates the Mermaid, and opens a **GitHub issue per changed diagram** containing the proposed fix as a diff. Nothing is committed or pushed. When everything matches, it is a no-op and no issue is created. |

The agent behavior is defined by two prompt templates:

- [`prompts/verify-sequence-diagrams.prompt.md`](prompts/verify-sequence-diagrams.prompt.md) — the audit itself.
- [`prompts/fix-mermaid-syntax.prompt.md`](prompts/fix-mermaid-syntax.prompt.md) — used by the lint self-correction loop to repair invalid Mermaid the audit may have introduced.

## Why issues instead of a pull request

Microsoft enterprise Actions security policy prevents this workflow from opening a PR:

- **User PATs are not usable for git/PR operations from Actions runners** — a push authenticated
  with a PAT is rejected with a `403` from the runner (it works from a developer machine, which is
  what makes this confusing).
- **"Allow GitHub Actions to create and approve pull requests" is disabled** at the enterprise
  level, so the default `GITHUB_TOKEN` cannot create a PR either.

Filing **issues** is not restricted by that policy: it needs only `GITHUB_TOKEN` with
`issues: write` (already declared in the workflow's `permissions` block). Each issue carries the
exact proposed diff, so a maintainer can implement it — for example by commenting
`@copilot implement the proposed fix in this issue`.

## Setup

### Create the `COPILOT_CLI_PAT` secret (model auth only)

The Copilot CLI needs a token to authenticate to the model. The default `GITHUB_TOKEN` has no
Copilot access, so a user Personal Access Token (PAT) with Copilot access is required. Unlike the
previous PR-based design, this token needs **no write access** — the workflow never pushes or
opens PRs.

1. Create a **fine-grained PAT**: <https://github.com/settings/personal-access-tokens/new>
   - **Resource owner**: your **personal account** (the "Copilot Requests" permission is only
     available on user-owned tokens).
   - **Repository access**: this repository (or *All repositories*).
   - **Permissions → Account**: `Copilot Requests` → **Read** (model auth).
   - **Permissions → Repository**: `Contents` → **Read** (lets the CLI read the checked-out repo
     via `gh`; no write is needed).
   - The token owner must have an **active Copilot subscription / seat**.
2. Add it as a repository secret named **`COPILOT_CLI_PAT`**:
   - *Settings → Secrets and variables → Actions → New repository secret*, or
   - `gh secret set COPILOT_CLI_PAT --repo <owner>/<repo>`

The workflow exposes the PAT to the CLI via `COPILOT_GITHUB_TOKEN` (model auth) and `GH_TOKEN`
(the CLI's own `gh`/`git` reads). Env precedence is
`COPILOT_GITHUB_TOKEN` > `GH_TOKEN` > `GITHUB_TOKEN`.

Issue creation uses the built-in `GITHUB_TOKEN` (no extra secret required). The `Documentation`
label is applied best-effort; if it doesn't exist, the issue is still filed (just unlabeled).

### (Optional) Adjust the schedule

The default cadence is Mondays at 08:00 UTC. Edit the `schedule.cron`
expression in [`verify-sequence-diagrams.yml`](workflows/verify-sequence-diagrams.yml)
to change it. You can always run it on demand from the **Actions** tab
(*Run workflow*).

## Usage

The workflow runs automatically each week. To run it now, open the
**Actions → Verify sequence diagrams** workflow and choose *Run workflow*.

If a diagram is out of date, an issue titled **"docs: sequence diagram drift in
`docs/<name>-sequence-diagram.md`"** is opened for each changed file, containing:

- the per-file line from the audit summary describing what drifted and why,
- the **proposed fix as a diff** (validated as parseable Mermaid before filing), and
- a call to action to implement it (e.g. `@copilot implement the proposed fix in this issue`).

To avoid noise, the workflow **skips filing** if an open issue with the same title already exists,
so a recurring weekly run will not create duplicates for drift that hasn't been addressed yet.

**Review the proposed change before applying it** — the audit is best-effort and the code is the
source of documentation truth.

## How it works

1. **Audit** — The Copilot CLI runs headless (`--allow-all-tools -p <prompt>`),
   reads every `docs/*sequence-diagram.md`, opens the source files referenced by
   each diagram's component tables, and edits **only** the markdown when it finds
   a real discrepancy backed by the code. It never touches anything under `src/`.
   The edits stay local — they are used to compute the proposed diff and are never committed.
2. **Lint + self-correct** — Each diagram is rendered with
   [`@mermaid-js/mermaid-cli`](https://github.com/mermaid-js/mermaid-cli) (`mmdc`). If the audit
   introduced invalid Mermaid, the exact parser errors are fed back to the Copilot CLI (using
   `fix-mermaid-syntax.prompt.md`) to repair the syntax, then the diagrams are re-linted. This
   repeats up to a bounded number of attempts (`max_fixups`). Only if Copilot still cannot produce
   valid Mermaid does the job fail — so an issue is never filed with an unparseable diagram.
3. **Issues** — For each `docs/*sequence-diagram.md` the audit actually changed
   (`git diff --name-only`), the workflow opens one issue with the audit note and the proposed
   diff. If nothing changed, no issue is created.

## Notes & tuning

- The agent is instructed to be **conservative** — it only changes a diagram when
  it can point to the specific code that proves the diagram is wrong, and it
  preserves each file's UTF-8 BOM, style, and level of detail.
- The proposed diff is wrapped in a **4-backtick** code fence so the diagrams' own ` ```mermaid `
  fences render correctly inside the issue body.
- To change *when* the agent runs, edit the `schedule`/`concurrency` settings in
  the workflow. To change *how* it behaves, edit
  [`prompts/verify-sequence-diagrams.prompt.md`](prompts/verify-sequence-diagrams.prompt.md) (audit)
  or [`prompts/fix-mermaid-syntax.prompt.md`](prompts/fix-mermaid-syntax.prompt.md) (syntax repair).
- To adjust how many times Copilot is asked to fix invalid Mermaid before the run fails, change
  `max_fixups` in the **Lint diagrams and self-correct** step.
