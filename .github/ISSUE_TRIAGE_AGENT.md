# AI Issue Triage Agent

Automated issue triage powered by the [GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/use-copilot-cli).

A single workflow drives the flow:

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`issue-triage-agent.yml`](workflows/issue-triage-agent.yml) | An issue is given the `ai-triage` label, **or** anyone comments on an `ai-triage` issue | Analyzes the issue and comments; asks the reporter for more info when needed; suggests a mitigation; and, when the problem is reproducible, posts a **ready-to-apply fix as an issue comment**. On follow-up comments it re-evaluates whether the new information refines the analysis and updates its comment(s) accordingly. |

This automation **only ever posts issue comments (and labels)** — it never
creates branches or pull requests, because that is prohibited in this org. When
the agent has a fix, it embeds it in a comment as a unified `diff` / full-file
patch that a maintainer can apply later.

The agent behavior is defined by the prompt template in
[`.github/prompts/issue-triage.md`](prompts/issue-triage.md).

## Setup

### 1. Create the `COPILOT_CLI_PAT` secret

The Copilot CLI needs a token for **two** things: authenticating to the model,
and performing GitHub operations (reading the issue and posting comments). The
default `GITHUB_TOKEN` cannot authenticate to Copilot, so a user Personal Access
Token (PAT) is required.

1. Create a **fine-grained PAT**: <https://github.com/settings/personal-access-tokens/new>
   - **Resource owner**: your **personal account** (the "Copilot Requests"
     permission is only available on user-owned tokens).
   - **Repository access**: this repository (or *All repositories*).
   - **Permissions → Account**: `Copilot Requests` → **Read** (model auth).
   - **Permissions → Repository**:
     - `Contents` → **Read** (read source to ground the analysis)
     - `Issues` → **Read and write** (comment, label)
   - The token owner must have an **active Copilot subscription / seat**.
2. Add it as a repository secret named **`COPILOT_CLI_PAT`**:
   - *Settings → Secrets and variables → Actions → New repository secret*, or
   - `gh secret set COPILOT_CLI_PAT --repo <owner>/<repo>`

The workflow exposes the PAT to the CLI via `COPILOT_GITHUB_TOKEN` (model auth)
and `GH_TOKEN` (gh). Env precedence is
`COPILOT_GITHUB_TOKEN` > `GH_TOKEN` > `GITHUB_TOKEN`.

### 2. Create the labels

```bash
gh label create ai-triage  --description "Run the Copilot triage agent" --color 1D76DB
gh label create needs-info --description "Waiting on more info from the reporter" --color FBCA04
```

## Usage

Add the **`ai-triage`** label to any issue. The agent runs and comments with its
analysis, and — when it can reproduce and fix the problem — posts a ready-to-apply
patch in a comment.

After that, **any new comment on the issue** re-runs the agent. If the comment
supplies missing details, corrects an assumption, or reports whether a mitigation
worked, the agent refines its analysis (and patch) in a follow-up comment. If the
comment doesn't change anything, the agent stays silent to avoid noise.

## Notes & tuning

- Every agent-authored comment is prefixed with
  `> _Automated triage by GitHub Copilot._`. The workflow's `if:` guard uses this
  prefix to skip the agent's own comments, preventing an infinite comment loop.
- To change *when* the agent acts, edit the `if:` guards in the workflow. To
  change *how* it behaves, edit the prompt template in `.github/prompts/`.
