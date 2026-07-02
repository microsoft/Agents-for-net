# Microsoft 365 Agents SDK - C# /.NET

The Microsoft 365 Agent SDK simplifies building full stack, multichannel, trusted agents for platforms including M365, Teams, Copilot Studio, and Webchat. We also offer integrations with 3rd parties such as Facebook Messenger, Slack, or Twilio. The SDK provides developers with the building blocks to create agents that handle user interactions, orchestrate requests, reason responses, and collaborate with other agents.

The M365 Agent SDK is a comprehensive framework for building enterprise-grade agents, enabling developers to integrate components from the Azure AI Foundry SDK, Semantic Kernel, as well as AI components from other vendors.

For more information please see the parent project information here [Microsoft 365 Agents SDK](https://aka.ms/agents)

## Current Project State is GENERALLY AVAILABLE (GA)

### Public Nuget feed.
The best way to consume this SDK is via our Nuget packages found here: [nuget.org](https://www.nuget.org/packages?q=microsoft.agents+AND+nugetbotbuilder&includeComputedFrameworks=true&prerel=false&sortby=relevance). They will all begin with **Microsoft.Agents**

### Nightly Nuget feed.
Nightly Feed has been shifted to public [nuget.org](https://www.nuget.org/profiles/nugetbotbuilder). They will all begin with **Microsoft.Agents**  and have a version number that ends with **-beta.**
- This feed is updated overnight (PT) whenever commits occur in our repo. 
- This feed's packages will be much more up to date with the current repo, however, packages provided on this feed are not necessarily stable.

## Working with this codebase

Please read [this](GettingStarted.md) for directions on what is needed and how to setup to build this codebase locally

## AI Coding Assistant Setup

### Agent Plugins (Skills)

This SDK provides AI coding assistant plugins that give your assistant deep knowledge of the Agents SDK APIs, patterns, and common mistakes. Skills activate automatically based on what you're working on.

The plugins are hosted in [microsoft/Agents — agent-plugins](https://github.com/microsoft/Agents/tree/main/agent-plugins).

**Available plugins for .NET:**

| Plugin | Skills Included |
|--------|----------------|
| `agents-sdk-common` | Azure provisioning, identity credentials, OAuth setup via `az` CLI |
| `agents-for-net` | Building agents in C#/.NET, debugging (auth failures, startup crashes), Bot Framework migration, ActivityHandler→AgentApplication migration |

**Installation (GitHub Copilot CLI or Claude Code):**

```
/plugin marketplace add microsoft/Agents
/plugin install agents-sdk-common@microsoft-agents-sdk
/plugin install agents-for-net@microsoft-agents-sdk
```

Skills activate automatically — no manual loading needed. Run `/plugin` to verify installation.

### Custom Agents (Code Review)

This repository includes custom agents in `.github/agents/` that reproduce the **GitHub Copilot PR review dynamic locally**, so you can run the reviewer-vs-challenger loop *before* opening a PR and skip the review round-trips:

| Agent | Model | Description |
|-------|-------|-------------|
| `review` | Claude Opus 4.8 | User-invocable **challenger**. Invokes `reviewer-github`, then disputes or resolves each finding with a per-finding verdict: **Fix / Push back / Needs judgment** (with paste-ready rebuttals). |
| `reviewer-github` | GPT-5.5 | **GitHub emulator** (different model, on purpose). Loads the exact instruction files GitHub Copilot code review reads and produces the findings GitHub would post. |

Both agents read the same instruction files GitHub uses, so the emulated review is realistic. The review rules are a **single source of truth**: they live in `.github/copilot-instructions.md` (repository-wide) and the `.github/instructions/*.instructions.md` files — `code-review.instructions.md` (repo-wide rules) plus the path-specific ones. Editing any of these changes what **both** GitHub's PR review and this local loop enforce, keeping them in sync.

#### How the loop works

1. **You invoke `review`** on your uncommitted changes or current branch (diff against `main`).
2. **`review` dispatches `reviewer-github`** (GPT-5.5) to emulate GitHub Copilot code review. It
   loads the same instruction files GitHub reads and returns the findings GitHub would post.
3. **`review` (Claude Opus 4.8) challenges each finding** — reading the actual code at HEAD and
   applying the anti-false-positive checks — then renders a per-finding verdict:
   - ✅ **Fix** — valid; address it before pushing (with a fix direction).
   - ⛔ **Push back** — false positive; you get a **paste-ready rebuttal** for the PR.
   - 🤔 **Needs judgment** — a subjective trade-off; you decide.

The result is that you arrive at the PR with fixes made and rebuttals ready, so GitHub's review has
little left to say — eliminating the review round-trips.

#### How to run it

Run this **before opening a PR**, on your current branch:

```bash
copilot --agent=review --prompt "Review my changes"
```

Or inside the GitHub Copilot CLI:

```
/agent                                      # Browse and select the `review` agent
Use the review agent to review my changes   # …or invoke it directly in a prompt
```

You can also point it at specific files or a commit, e.g. `Review commit <sha>` or
`Review src/libraries/.../Foo.cs`.

#### How to customize the rules

The review rules are a **single source of truth**, so local review and GitHub's PR review never
diverge. To change what gets flagged, edit the instruction files — **not** the agents:

- **Repository-wide rules:** `.github/instructions/code-review.instructions.md` (`applyTo: "**"`).
  Add, remove, or tighten lenses and anti-false-positive checks here.
- **Subsystem-specific rules:** the path-specific `*.instructions.md` files (they apply only when a
  changed file matches their `applyTo` globs).

Because GitHub Copilot code review reads these same files, any edit changes **both** GitHub's PR
review and this local loop at once. (Note: organization-level instructions set in GitHub org
settings are not stored in the repo, so the local emulator cannot see them.)

The agents are tailored to this codebase — they understand System.Text.Json serialization,
multi-targeting (net8.0/netstandard2.0), Central Package Management, the Activity Protocol, and the
layered library architecture, all via the shared instruction files.

### Contextual Instructions

Path-scoped instruction files in `.github/instructions/` provide AI assistants with architectural context (including mermaid sequence diagrams) that activates automatically when working on relevant code:

| Instruction File | Activates For |
|-----------------|---------------|
| `code-review.instructions.md` | All files — repository-wide code review rules (edit to customize what GitHub Copilot and the local `review` agent enforce) |
| `oauth-flows.instructions.md` | UserAuth, Authentication, OAuth, SignIn code |
| `cloudadapter-pipeline.instructions.md` | CloudAdapter, Hosting/AspNetCore, TurnContext, Middleware |
| `streaming-response.instructions.md` | StreamingResponse, StreamInfo, LLMClient code |
| `serialization-extension.instructions.md` | ProtocolJsonSerializer, Serialization/Converters, Entity/Serialization init |
| `proactive-messaging.instructions.md` | Proactive, ContinueConversation, Conversation builders |

## Support

**See [Support.md](support.md) for details**

## Contributing

#### Note for Microsoft internal developers: 
- Internal Microsoft Developers should join the Core identity group [Agents SDK Contrib](https://coreidentity.microsoft.com/manage/Entitlement/entitlement/agentssdkint-upyj)

#### Non-Microsoft internal developers:

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

## Useful Links

- [agents Repository](https://github.com/Microsoft/Agents)
- agents-for-net Repository: **You are here.**
- [agents-for-js Repository](https://github.com/Microsoft/Agents-for-js)
- [agents-for-python Repository]( https://github.com/Microsoft/Agents-for-python)
- [Official Agents Documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- [.NET Documentation](https://learn.microsoft.com/en-us/dotnet/api/?view=m365-agents-sdk&preserve-view=true)
