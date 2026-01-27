# Microsoft 365 Agents SDK for .NET - Release Notes v1.4.0

**Release Date:** January 27, 2026
**Previous Version:** 1.3.0 (Released October 22, 2025)

## 🎉 What's New in 1.4.0

This release focuses on enhanced authentication capabilities, improved telemetry and diagnostics, better multi-tenant support for agentic scenarios, and numerous quality-of-life improvements. Key highlights include exchangeable SSO token support, Activity telemetry management, M365 attachment handling, and extensive bug fixes and optimizations.

## 🚀 Major Features & Enhancements

### Enhanced Authentication & SSO
- **Exchangeable Token Support**: Fixed `TokenResponse.IsExchangeable` property handling to properly preserve exchangeable token state during SSO token exchanges. ([#662](https://github.com/microsoft/Agents-for-net/pull/662))
- **Multi-Tenant Agentic Authentication**: Enhanced support for multi-tenant scenarios in agentic contexts, including proper handling of "common" authority endpoints and tenant-specific token acquisition. ([#500](https://github.com/microsoft/Agents-for-net/pull/500), [#626](https://github.com/microsoft/Agents-for-net/pull/626))
- **MSAL Authority Improvements**: Updated `AgentUserAuthorization` to use `ConfidentialClientApplicationBuilder` and improved authority resolution for better compliance with Azure Active Directory patterns. ([#626](https://github.com/microsoft/Agents-for-net/pull/626))
- **Token Event Handling**: `AgentUserAuthorization` now accepts Event-based tokens and responses for improved token flow handling. ([#529](https://github.com/microsoft/Agents-for-net/pull/529))
- **Token Exchange Fix**: Resolved issue where `UserAuthorization.ExchangeToken` was using cached tokens for subsequent exchanges instead of fresh tokens. ([#622](https://github.com/microsoft/Agents-for-net/pull/622))
- **Null Scopes Handling**: Improved robustness when handling null scopes in MSAL authentication scenarios. ([#467](https://github.com/microsoft/Agents-for-net/pull/467))
- **TokenResponse Expiration**: Only set `Expiration` in `TokenResponse` when a non-default value is present, preventing incorrect expiration timestamps. ([#544](https://github.com/microsoft/Agents-for-net/pull/544))

### Telemetry & Diagnostics
- **Activity Telemetry Management**: Introduced comprehensive Activity telemetry support including proper cloning of `Activity.Current` for background queue processing to maintain distributed tracing context. ([#630](https://github.com/microsoft/Agents-for-net/pull/630))
- **Renamed TelemetryActivity**: Renamed `DiagnosticsActivity` to `TelemetryActivity` for clarity and consistency with OpenTelemetry conventions. ([#630](https://github.com/microsoft/Agents-for-net/pull/630))
- **Activity Lifecycle Management**: Ensured `TelemetryActivity` is properly stopped after activity processing completes, preventing resource leaks. ([#630](https://github.com/microsoft/Agents-for-net/pull/630))
- **Updated Activity Cloning**: Modified Activity cloning to use `TraceId`/`SpanId` and removed start time copying for better distributed tracing correlation. ([#630](https://github.com/microsoft/Agents-for-net/pull/630))
- **Enhanced Logging**: Added debug logging for incoming activities and improved error logging throughout the SDK. ([#495](https://github.com/microsoft/Agents-for-net/pull/495), [#483](https://github.com/microsoft/Agents-for-net/pull/483))

### Streaming Response Improvements
- **Public StreamId Property**: Exposed `IStreamingResponse.StreamId` as a public property for better streaming response tracking and correlation. ([#631](https://github.com/microsoft/Agents-for-net/pull/631))
- **Teams Agentic Streaming**: Disabled streaming responses when channel is Teams and request type is agentic, addressing platform compatibility requirements. ([#517](https://github.com/microsoft/Agents-for-net/pull/517))

### M365 & Teams Enhancements
- **M365AttachmentDownloader**: Renamed `TeamsAttachmentDownloader` to `M365AttachmentDownloader` and moved it to the Core library for broader applicability across Microsoft 365 channels. ([#492](https://github.com/microsoft/Agents-for-net/pull/492))
- **M365 Copilot Channel Support**: Extended attachment downloader to support M365Copilot channels in addition to Teams. ([#492](https://github.com/microsoft/Agents-for-net/pull/492))
- **Improved Token Provider Access**: Updated `M365AttachmentDownloader` to use `IConnection.GetTokenProvider` for proper per-request token provider access. ([#492](https://github.com/microsoft/Agents-for-net/pull/492))
- **Teams Models & Aspect Ratios**: Added comprehensive Teams model definitions including `AspectRatios` constants and improved model code quality. ([#574](https://github.com/microsoft/Agents-for-net/pull/574))
- **Teams Extension Agentic Support**: Added agentic route flag support to Teams `AgentExtension` for proper routing in agentic scenarios. ([#561](https://github.com/microsoft/Agents-for-net/pull/561))

### Storage Enhancements
- **BlobsTranscriptStore Improvements**: Exposed constructor that accepts a `BlobContainerClient` for better integration with existing Azure SDK patterns and dependency injection. ([#559](https://github.com/microsoft/Agents-for-net/pull/559))
- **CosmosDB Options Fix**: Fixed validation logic to not require client-related `CosmosDbPartitionedStorageOptions` properties when a `CosmosClient` instance is provided directly. ([#494](https://github.com/microsoft/Agents-for-net/pull/494))

## 📚 Documentation & Developer Experience

### API & Configuration Changes
- **Obsoleted AgentApplicationOptions.Adapter**: Marked `AgentApplicationOptions.Adapter` property as obsolete to guide developers toward updated configuration patterns. ([#639](https://github.com/microsoft/Agents-for-net/pull/639))
- **Contributing Guidelines**: Updated README.md with improved contributing guidelines and project information. ([#508](https://github.com/microsoft/Agents-for-net/pull/508))

### Error Handling & Diagnostics
- **Explicit Error Codes**: Made all error codes explicit and durable across `ErrorHelper` classes with readonly modifiers for better maintainability. ([#506](https://github.com/microsoft/Agents-for-net/pull/506))
- **Updated Error Code Ranges**: Changed error code range from -60000 to -50500 in Connector `ErrorHelper` for better organization. ([#512](https://github.com/microsoft/Agents-for-net/pull/512))
- **Error Link Updates**: Updated error links to use `M365AgentsErrorCodes` with hashtags for improved error documentation navigation. ([#510](https://github.com/microsoft/Agents-for-net/pull/510))
- **Improved Error Messages**: Enhanced token provider error messages for better debugging experience. ([#500](https://github.com/microsoft/Agents-for-net/pull/500))
- **Fixed Duplicate Error Codes**: Resolved duplicate error code for `UserAuthorizationDefaultHandlerNotFound`. ([#506](https://github.com/microsoft/Agents-for-net/pull/506))

## 🔧 Developer Tools & Quality

### Serialization & Performance
- **EntityInitSourceGenerator**: Added source generator for entity initialization, improving cold-start performance by eliminating runtime assembly scans. ([#593](https://github.com/microsoft/Agents-for-net/pull/593))
- **PreloadAssembliesSourceGenerator**: Added source generator that creates `ModuleInitializer` methods to preload required assemblies, further reducing startup time. ([#593](https://github.com/microsoft/Agents-for-net/pull/593))
- **Entity Handling Separation**: Separated preload assembly logic from entity handling for cleaner architecture. ([#593](https://github.com/microsoft/Agents-for-net/pull/593))
- **EntityConverter Refactoring**: Refactored `EntityConverter` for improved maintainability and performance. ([#593](https://github.com/microsoft/Agents-for-net/pull/593))
- **EntityInitAssemblyAttribute**: Added assembly-level attribute for entity initialization metadata. ([#593](https://github.com/microsoft/Agents-for-net/pull/593))

### Code Quality & Analysis
- **Analyzer Improvements**: Enhanced analyzer infrastructure and refactored for better code quality detection. ([#451](https://github.com/microsoft/Agents-for-net/pull/451))
- **CodeQL Security Fix**: Added validation for path characters in zip entry names to prevent path traversal vulnerabilities. ([#657](https://github.com/microsoft/Agents-for-net/pull/657))
- **File Path Security**: Fixed security issue in `TranscriptUtilities` test helper to prevent arbitrary file access, ensuring compatibility with .NET Framework 4.8 and newer. ([#490](https://github.com/microsoft/Agents-for-net/pull/490))

### Build & CI/CD
- **Skip Dependabot Builds**: Added condition to skip Azure DevOps builds triggered by Dependabot for reduced CI noise. ([#654](https://github.com/microsoft/Agents-for-net/pull/654))
- **Dependabot Grouping**: Configured Dependabot to group all dependency updates and dotnet-sdk updates together. ([#654](https://github.com/microsoft/Agents-for-net/pull/654))
- **.NET SDK Update**: Bumped .NET SDK from 8.0.415 to 8.0.416 for latest servicing updates. ([#515](https://github.com/microsoft/Agents-for-net/pull/515))
- **Dependency Updates**: Multiple dependency rollups including System.Text.Json, Microsoft.Identity.Web.Certificateless, and Google.Protobuf. ([#641](https://github.com/microsoft/Agents-for-net/pull/641), [#602](https://github.com/microsoft/Agents-for-net/pull/602), [#552](https://github.com/microsoft/Agents-for-net/pull/552))
- **.gitignore Updates**: Added Claude Code temporary files to .gitignore. ([#654](https://github.com/microsoft/Agents-for-net/pull/654))
- **CODEOWNERS**: Added CODEOWNERS file for automatic review request assignments. ([#488](https://github.com/microsoft/Agents-for-net/pull/488))

## 🐛 Bug Fixes & Maintenance

### Authentication & Identity Fixes
- **Recipient.Id Validation**: Relaxed `Recipient.Id` requirement for Microsoft Copilot Studio scenarios to accommodate platform-specific patterns. ([#489](https://github.com/microsoft/Agents-for-net/pull/489))
- **Tenant ID Preference**: In agentic scenarios, now preferring `Recipient.TenantId` over `Conversation.TenantId` for more accurate tenant resolution. ([#500](https://github.com/microsoft/Agents-for-net/pull/500))
- **Case-Insensitive Role Comparisons**: Implemented case-insensitive role comparisons to handle role claim variations. ([#499](https://github.com/microsoft/Agents-for-net/pull/499))
- **AgentClaims.CreateIdentity**: Corrected identity creation logic and improved documentation. ([#487](https://github.com/microsoft/Agents-for-net/pull/487))

### Activity & Communication Fixes
- **TurnContext.SendActivities Null Handling**: Added proper null checking in `TurnContext.SendActivities` to prevent null reference exceptions. ([#589](https://github.com/microsoft/Agents-for-net/pull/589))
- **Activity.Id Capture Fix**: Reverted removal of Activity.Id capture during `TurnContext.SendActivities` to maintain proper activity tracking. ([#589](https://github.com/microsoft/Agents-for-net/pull/589))
- **Conversation ID URL Encoding**: URL-encoded conversation IDs in `ReplyToActivity` and `SendToConversation` to handle special characters correctly. ([#532](https://github.com/microsoft/Agents-for-net/pull/532))
- **Obsolete ContinueConversation Methods**: Marked some Adapter.ContinueConversation method overloads as obsolete and improved proactive conversation handling. ([#487](https://github.com/microsoft/Agents-for-net/pull/487))

### Routing & Configuration Fixes
- **Agentic Route Flag**: Fixed issue where agentic flag was not being passed to `RouteList` during `AddRoute` operations. ([#535](https://github.com/microsoft/Agents-for-net/pull/535))
- **Removed Dead Property**: Removed unused `AgentApplication.AgentAuthorization` property. ([#499](https://github.com/microsoft/Agents-for-net/pull/499))
- **OBO Sample Cleanup**: Removed duplicate per-route OAuth configuration in OBO sample. ([#560](https://github.com/microsoft/Agents-for-net/pull/560))

### Error Handling Fixes
- **Simplified Error Handling**: Improved error handling in `ChannelAdapter.cs` for clearer exception propagation. ([#498](https://github.com/microsoft/Agents-for-net/pull/498))
- **Streaming Exception Handling**: Enhanced exception handling in streaming scenarios. ([#478](https://github.com/microsoft/Agents-for-net/pull/478))

### Sample & Test Fixes
- **HandlingAttachments Sample**: Corrected Teams manifest and simplified HandlingAttachments sample implementation. ([#492](https://github.com/microsoft/Agents-for-net/pull/492))
- **Agentic Unit Tests**: Fixed unit tests for agentic scenarios. ([#626](https://github.com/microsoft/Agents-for-net/pull/626))
- **File Downloader Timing**: Fixed `M365AttachmentDownloader` to run prior to `OnBeforeTurn` handlers. ([#492](https://github.com/microsoft/Agents-for-net/pull/492))

### Miscellaneous Fixes
- **Using Statement Cleanup**: Added proper using statements in Agentic HTTP requests to ensure proper resource disposal. ([#500](https://github.com/microsoft/Agents-for-net/pull/500))
- **Argument Checking**: Added comprehensive argument validation throughout the codebase. ([#559](https://github.com/microsoft/Agents-for-net/pull/559))
- **Copyright Cleanup**: Removed duplicate copyright notices from analyzer helper code. ([#593](https://github.com/microsoft/Agents-for-net/pull/593))

## 🚀 Getting Started

Upgrade your projects to the new release with:

```powershell
dotnet add package Microsoft.Agents.Hosting.AspNetCore --version 1.4.0
dotnet add package Microsoft.Agents.Authentication.Msal --version 1.4.0
```

## ⚠️ Breaking Changes & Migration Notes

- `TeamsAttachmentDownloader` has been renamed to `M365AttachmentDownloader` and moved to `Microsoft.Agents.Core`. Update your using statements and class references accordingly.
- `AgentApplicationOptions.Adapter` is now marked as obsolete. Review your configuration code and migrate to the recommended patterns.
- Some `Adapter.ContinueConversation` method overloads are now obsolete. Update to use the current method signatures.

## 🙏 Acknowledgments

Thank you to the Microsoft 365 Agents team and the open-source community for the ideas, code reviews, and contributions that shaped this release. Special thanks to contributors including Tracy Boehrer, Matt Barbour, Chris Mullins, and the Copilot SWE agent for numerous improvements and fixes.

## 📞 Support & Resources

- **Documentation:** [Microsoft 365 Agents SDK](https://aka.ms/agents)
- **Issues:** [GitHub Issues](https://github.com/microsoft/Agents-for-net/issues)
- **Samples:** [Agent Samples Repository](https://github.com/microsoft/Agents)
- **Community:** Join the discussions and share feedback through GitHub.

---

# Microsoft 365 Agents SDK for .NET - Release Notes v1.3.0

**Release Date:** October 22, 2025
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
