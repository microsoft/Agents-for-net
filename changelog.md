# Microsoft 365 Agents SDK for .NET - Release Notes v1.3.0

**Release Date:** November 15, 2025  
**Previous Version:** 1.2.0 (Released August 18, 2025)

## 🎉 What's New in 1.3.0

This release introduces first-class A2A hosting (preview), and Copilot Studio Connector (preview). It also brings extensible serialization, richer feedback orchestration, and bug fixes to help you build production-grade agent experiences.

## 🚀 Major Features & Enhancements

### A2A Hosting (Preview)
- Introduced the `Microsoft.Agents.Hosting.AspNetCore.A2A.Preview` library for support in exposting your SDK agent to A2A clients. ([#391](https://github.com/microsoft/Agents-for-net/pull/391))
- Added A2A sample agent (`samples/A2AAgent`, `samples/A2ATCKAgent`) that demonstrate basic A2A multi-turn tasks, and alignment with the A2A TCK. ([#391](https://github.com/microsoft/Agents-for-net/pull/391))

### Copilot Studio Agent Connector
- Added the Copilot Studio Power Apps Connector implementation, including and a ready-to-run sample. This is in preview in Copilot Studio and not generally available for everyone. ([#450](https://github.com/microsoft/Agents-for-net/pull/450))
- Samples now default to requiring authentication and respect configured scopes when calling Copilot Studio services. ([#472](https://github.com/microsoft/Agents-for-net/pull/472), [#450](https://github.com/microsoft/Agents-for-net/pull/450))

### Citation-Aware Responses
- Improved streaming responses with citation management APIs, deduplication safeguards, and richer metadata on AI entities. ([#427](https://github.com/microsoft/Agents-for-net/pull/427))
- Added helper methods to `MessageFactory` and entity models so experiences can surface citations without hand-coding payloads. ([#427](https://github.com/microsoft/Agents-for-net/pull/427))
 
### Feedback Loop
- Enabled feedback loop handling in AgentApplication, making it easier to capture user evaluations during conversations. This existed in the Teams Extension, but is now part of AgentApplication.  Not all channels support this, but expanded support is coming. ([#480](https://github.com/microsoft/Agents-for-net/pull/480))

## 📚 Documentation & Developer Experience

### Expanded API Surface Descriptions
- Added missing XML documentation for `AgentApplicationBuilder`, `IAgentClient`, `IAgentHost`, and quick view responses to improve IntelliSense and API discovery. ([#417](https://github.com/microsoft/Agents-for-net/pull/417))
- Clarified Teams channel support semantics in `Channels` to avoid confusion when routing agent traffic. ([#430](https://github.com/microsoft/Agents-for-net/pull/430))

## 🔧 Developer Tools & Quality

### Serialization Performance & Extensibility
- Generated assembly-level serialization attributes to eliminate runtime scans and lowered cold-start costs. ([#449](https://github.com/microsoft/Agents-for-net/pull/449), [#441](https://github.com/microsoft/Agents-for-net/pull/441))
- Introduced dynamic entity discovery via `EntityNameAttribute` and public registration hooks, enabling custom entity types without forking the serializer. ([#465](https://github.com/microsoft/Agents-for-net/pull/465))
- Refined entity cleanup and removal logic to prevent stale data from leaking between turns. ([#463](https://github.com/microsoft/Agents-for-net/pull/463))

### Analyzer & Build Infrastructure
- Bundled analyzers inside `Microsoft.Agents.Core` and unified their target framework for consistent diagnostics. ([37deeaf7](https://github.com/microsoft/Agents-for-net/commit/37deeaf7f2be0ea13cb02c45e2c13451d5ebf593), [5fa356d3](https://github.com/microsoft/Agents-for-net/commit/5fa356d3f31942f7ce06107b809b65f900a3f609))
- Moved the repository to .NET SDK 8.0.414 to align with the latest LTS servicing updates. ([#439](https://github.com/microsoft/Agents-for-net/pull/439), [#481](https://github.com/microsoft/Agents-for-net/pull/481))

## 🔐 Authentication & Security Enhancements

- Add support for Entra Agent ID Preview
- Improved Multi-tenant detction for authenticaiton token support this will attempt to create a tenant specifice token for User Autheitcation and AgentID scenarios when the authority associated with current Agent identity is using the /common authority.5

## 🐛 Bug Fixes & Maintenance

- Resolved failures when registering `IAgent` implementations via factories in multi-agent hosts. ([#418](https://github.com/microsoft/Agents-for-net/pull/418))
- Ensured Cosmos DB storage can reuse existing `CosmosClient` instances for dependency injection scenarios. ([#446](https://github.com/microsoft/Agents-for-net/pull/446))
- Improved route handlers to understand sub-channel identifiers and alternate blueprint connection names. ([#458](https://github.com/microsoft/Agents-for-net/pull/458), [#445](https://github.com/microsoft/Agents-for-net/pull/445))
- Removed obsolete helper extensions and tightened activity validation to match the latest Teams schemas. ([#461](https://github.com/microsoft/Agents-for-net/pull/461))

## 📦 New Package Information

1. **Microsoft.Agents.Hosting.AspNetCore.A2A.Preview** – new preview package delivering A2A hosting (preview). ([#391](https://github.com/microsoft/Agents-for-net/pull/391))

## 🚀 Getting Started

Upgrade your projects to the new release with:

```powershell
dotnet add package Microsoft.Agents.Hosting.AspNetCore --version 1.3.0
dotnet add package Microsoft.Agents.Authentication.Msal --version 1.3.0
```

## 🙏 Acknowledgments

Thank you to the Microsoft 365 Agents team and the open-source community for the ideas, code reviews, and contributions that shaped this release.

## 📞 Support & Resources

- **Documentation:** [Microsoft 365 Agents SDK](https://aka.ms/agents)
- **Issues:** [GitHub Issues](https://github.com/microsoft/Agents-for-net/issues)
- **Samples:** [Agent Samples Repository](https://github.com/microsoft/Agents)
- **Community:** Join the discussions and share feedback through GitHub.
