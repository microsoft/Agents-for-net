# Sequence Diagram Verification Agent

Automated, weekly verification of the Mermaid sequence diagrams under [`docs/`](../docs)
against the actual implementation, powered by the
[GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/use-copilot-cli).

One workflow drives the flow:

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`verify-sequence-diagrams.yml`](workflows/verify-sequence-diagrams.yml) | Weekly schedule (Mondays 08:00 UTC) or manual `workflow_dispatch` | Reads each `docs/*sequence-diagram.md`, follows its "Key Components"/"Participants" tables into `src/`, and checks the diagram against the code (renamed/removed methods, call ordering, control-flow branches, stale file paths). When a diagram has drifted, it corrects the markdown, lints the Mermaid, and opens a **PR** with the changes. When everything matches, it is a no-op and no PR is created. |

The agent behavior is defined by the prompt template in
[`.github/prompts/verify-sequence-diagrams.prompt.md`](prompts/verify-sequence-diagrams.prompt.md).

## Setup

### 1. Create the `COPILOT_CLI_PAT` secret

The Copilot CLI needs a token for **two** things: authenticating to the model,
and performing GitHub operations (creating the branch and PR). The default
`GITHUB_TOKEN` cannot do either — it has no Copilot access, and pushes/PRs made
with it do **not** trigger downstream workflows (such as CI on the generated PR).
So a user Personal Access Token (PAT) is required.

1. Create a **fine-grained PAT**: <https://github.com/settings/personal-access-tokens/new>
   - **Resource owner**: your **personal account** (the "Copilot Requests"
     permission is only available on user-owned tokens).
   - **Repository access**: this repository (or *All repositories*).
   - **Permissions → Account**: `Copilot Requests` → **Read** (model auth).
   - **Permissions → Repository**:
     - `Contents` → **Read and write** (create the branch, push doc commits)
     - `Pull requests` → **Read and write** (open the PR)
   - The token owner must have an **active Copilot subscription / seat**.
2. Add it as a repository secret named **`COPILOT_CLI_PAT`**:
   - *Settings → Secrets and variables → Actions → New repository secret*, or
   - `gh secret set COPILOT_CLI_PAT --repo <owner>/<repo>`

The workflow exposes the PAT to the CLI via `COPILOT_GITHUB_TOKEN` (model auth)
and `GH_TOKEN` (gh/git), and passes it to `peter-evans/create-pull-request` so
that CI runs on the generated PR. Env precedence is
`COPILOT_GITHUB_TOKEN` > `GH_TOKEN` > `GITHUB_TOKEN`.

### 2. Allow Actions to create pull requests

*Settings → Actions → General → Workflow permissions* → enable
**"Allow GitHub Actions to create and approve pull requests"**.

### 3. (Optional) Adjust the schedule

The default cadence is Mondays at 08:00 UTC. Edit the `schedule.cron`
expression in [`verify-sequence-diagrams.yml`](workflows/verify-sequence-diagrams.yml)
to change it. You can always run it on demand from the **Actions** tab
(*Run workflow*).

## Usage

The workflow runs automatically each week. To run it now, open the
**Actions → Verify sequence diagrams** workflow and choose *Run workflow*.

If any diagram is out of date, a PR titled
**"docs: sync sequence diagrams with implementation"** is opened on the
`docs/sequence-diagram-sync` branch, labeled `Documentation`, with a per-file
audit summary in the description. **Review it before merging** — the audit is
best-effort and the diagrams are the source of documentation truth.

## How it works

1. **Audit** — The Copilot CLI runs headless (`--allow-all-tools -p <prompt>`),
   reads every `docs/*sequence-diagram.md`, opens the source files referenced by
   each diagram's component tables, and edits **only** the markdown when it finds
   a real discrepancy backed by the code. It never touches anything under `src/`.
2. **Lint** — Each diagram is rendered with
   [`@mermaid-js/mermaid-cli`](https://github.com/mermaid-js/mermaid-cli)
   (`mmdc`) to guarantee the Mermaid syntax is valid. If any diagram fails to
   parse, the run fails and **no PR is opened**, so a broken diagram is never
   proposed.
3. **PR** — The agent emits a `=== DIAGRAM AUDIT SUMMARY ===` block that becomes
   the PR body, and `peter-evans/create-pull-request` commits only the
   `docs/*sequence-diagram.md` files. If nothing changed, no PR is created.

## Notes & tuning

- The agent is instructed to be **conservative** — it only changes a diagram when
  it can point to the specific code that proves the diagram is wrong, and it
  preserves each file's UTF-8 BOM, style, and level of detail.
- To change *when* the agent runs, edit the `schedule`/`concurrency` settings in
  the workflow. To change *how* it behaves, edit
  [`.github/prompts/verify-sequence-diagrams.prompt.md`](prompts/verify-sequence-diagrams.prompt.md).
- The branch name (`docs/sequence-diagram-sync`) is reused each run;
  `delete-branch: true` cleans it up after the PR is merged or closed.
