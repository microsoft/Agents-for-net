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

This repository includes custom agents in `.github/agents/` for multi-model adversarial code review:

| Agent | Model | Description |
|-------|-------|-------------|
| `review` | Claude Sonnet 4.5 | User-invocable coordinator — triggers both reviewers and synthesizes findings |
| `reviewer-opus` | Claude Opus 4.8 | Adversarial reviewer (high-reasoning lens) |
| `reviewer-gpt` | GPT-5.5 | Independent second-model reviewer |

**Usage in GitHub Copilot CLI:**

```
/agent              # Browse and select from available agents
```

Or reference it directly in a prompt:

```
Use the review agent to review my current changes
```

Or from the command line:

```bash
copilot --agent=review --prompt "Review my changes"
```

The reviewers are tailored to this codebase — they understand System.Text.Json serialization, multi-targeting (net8.0/netstandard2.0), Central Package Management, the Activity Protocol, and the layered library architecture.

### Contextual Instructions

Path-scoped instruction files in `.github/instructions/` provide AI assistants with architectural context (including mermaid sequence diagrams) that activates automatically when working on relevant code:

| Instruction File | Activates For |
|-----------------|---------------|
| `oauth-flows.instructions.md` | UserAuth, Authentication, OAuth, SignIn code |
| `cloudadapter-pipeline.instructions.md` | CloudAdapter, Hosting/AspNetCore, TurnContext, Middleware |
| `streaming-response.instructions.md` | StreamingResponse, StreamInfo, LLMClient code |

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
