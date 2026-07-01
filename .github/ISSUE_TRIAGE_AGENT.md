# AI Issue Triage Agent

Automated issue triage powered by the [GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/use-copilot-cli).

Two workflows drive the flow:

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`issue-triage-agent.yml`](workflows/issue-triage-agent.yml) | An issue is given the `ai-triage` label | Analyzes the issue and comments; asks the reporter for more info when needed; suggests a mitigation; and, when the problem is reproducible, opens a **draft PR** with a fix and requests a Copilot review. |
| [`copilot-review-response.yml`](workflows/copilot-review-response.yml) | Copilot submits a review / review comment on an `ai-triage/*` PR | **Resolves** (applies + replies) or **disputes** (replies with rationale) each Copilot suggestion. |

The agent behavior is defined by the prompt templates in
[`.github/prompts/`](prompts/).

## Setup

### 1. Create the `COPILOT_CLI_PAT` secret

The Copilot CLI needs a token for **two** things: authenticating to the model,
and performing GitHub operations (comments, branches, PRs). The default
`GITHUB_TOKEN` cannot do either — it has no Copilot access, and pushes/PRs made
with it do **not** trigger downstream workflows (such as Copilot review). So a
user Personal Access Token (PAT) is required.

1. Create a **fine-grained PAT**: <https://github.com/settings/personal-access-tokens/new>
   - **Resource owner**: your **personal account** (the "Copilot Requests"
     permission is only available on user-owned tokens).
   - **Repository access**: this repository (or *All repositories*).
   - **Permissions → Account**: `Copilot Requests` → **Read** (model auth).
   - **Permissions → Repository**:
     - `Contents` → **Read and write** (create branches, push fix commits)
     - `Issues` → **Read and write** (comment, label)
     - `Pull requests` → **Read and write** (open draft PRs, request reviewers)
   - The token owner must have an **active Copilot subscription / seat**.
2. Add it as a repository secret named **`COPILOT_CLI_PAT`**:
   - *Settings → Secrets and variables → Actions → New repository secret*, or
   - `gh secret set COPILOT_CLI_PAT --repo <owner>/<repo>`

The workflows expose the PAT to the CLI via `COPILOT_GITHUB_TOKEN` (model auth)
and `GH_TOKEN` (gh/git). Env precedence is
`COPILOT_GITHUB_TOKEN` > `GH_TOKEN` > `GITHUB_TOKEN`.

### 2. Create the labels

```bash
gh label create ai-triage  --description "Run the Copilot triage agent" --color 1D76DB
gh label create needs-info --description "Waiting on more info from the reporter" --color FBCA04
```

### 3. Enable Copilot code review

Make sure **Copilot code review** is enabled for the repository (or org) so the
draft PR gets reviewed and the response workflow has something to react to. See
[Configuring automatic code review by Copilot](https://docs.github.com/en/copilot/how-tos/use-copilot-for-common-tasks/use-copilot-code-review).

### 4. Allow Actions to create pull requests

*Settings → Actions → General → Workflow permissions* → enable
**"Allow GitHub Actions to create and approve pull requests"**.

## Usage

Add the **`ai-triage`** label to any issue. The agent runs, comments with its
analysis, and (when it can reproduce and fix the problem) opens a draft PR
targeting the default branch that `Closes` the issue.

## Notes & tuning

- Every agent-authored comment is prefixed with
  `> _Automated triage by GitHub Copilot._`.
- The fix branch naming (`ai-triage/issue-<n>`) is what links the two workflows —
  the response workflow only reacts to PRs on that branch prefix.
- The reviewer login used to request Copilot review
  (`copilot-pull-request-reviewer[bot]`) and the exact request-review API can
  vary by org configuration; the triage prompt includes a `@copilot` fallback.
- To change *when* the agent acts, edit the `if:` guards in the workflows. To
  change *how* it behaves, edit the prompt templates in `.github/prompts/`.
