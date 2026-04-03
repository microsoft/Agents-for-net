# Teams Analyzer Extension Design

**Date:** 2026-04-03
**Status:** Approved
**Scope:** `Microsoft.Agents.Extensions.Teams.Analyzers` + test project

---

## Problem

The existing `TeamsRouteAttributeAnalyzer` validates method signatures for 14 Teams route attributes (message extension + meetings). Three attribute groups are not covered:

- **TaskModules** — `FetchRouteAttribute`, `SubmitRouteAttribute`
- **TeamsChannels** — 9 `Channel*RouteAttribute` variants
- **TeamsTeams** — 7 `Team*RouteAttribute` variants

Additionally, five message extension attributes accept two mutually exclusive constructor parameters (`commandId` and `commandIdPattern`) with no compile-time enforcement — both can be set silently, causing the second to be ignored at runtime.

---

## Goals

1. Add signature validation coverage for all 18 missing Teams route attributes.
2. Add a new compile-time error (MTEAMS004) when both `commandId` and `commandIdPattern` are provided on the same attribute.

---

## Non-Goals

- No changes to the `SignatureRule` type or `IsTypeCompatible`/`IsExpectedReturnType` helpers.
- No new analyzer classes or projects.
- No Builder-library attribute coverage (separate work item).

---

## Shared Type Metadata Names

These strings are used verbatim as `ParameterTypes` entries in `SignatureRule`:

| Logical name | Metadata name string |
|---|---|
| `ITurnContext` | `Microsoft.Agents.Builder.ITurnContext` |
| `ITurnState` | `Microsoft.Agents.Builder.State.ITurnState` |
| `CancellationToken` | `System.Threading.CancellationToken` |
| `Channel` | `Microsoft.Teams.Api.Channel` |
| `Team` | `Microsoft.Teams.Api.Team` |
| `TaskModules.Request` | `Microsoft.Teams.Api.TaskModules.Request` |
| `TaskModules.Response` | `Microsoft.Teams.Api.TaskModules.Response` |

---

## Design

### Approach

Extend `TeamsRouteAttributeAnalyzer.cs` directly. All changes are additive to the existing single-file analyzer.

### New Diagnostic: MTEAMS004

```
ID:            MTEAMS004
Constant name: MutualExclusivityDiagnosticId
Severity:      Error
Category:      Usage
Title:         Mutually exclusive attribute arguments
Message:       Method '{0}' decorated with '[{1}]' cannot specify both
               'commandId' and 'commandIdPattern' — they are mutually exclusive
```

Reported using `method.Locations[0]` (same as MTEAMS001–003) — the method's first declared source location.

`SupportedDiagnostics` updated to include `MutualExclusivityDescriptor`.

`AnalyzerReleases.Unshipped.md` gets this row appended to the existing table:

```
MTEAMS004 | Usage | Error | TeamsRouteAttributeAnalyzer
```

---

### New Signature Rules (18 entries)

All entries are appended to the existing `Rules` `ImmutableArray<SignatureRule>`.

#### TaskModules (2)

Both attributes have identical signatures. `ReturnTypeGenericArgument` = `"Microsoft.Teams.Api.TaskModules.Response"`.

| `AttributeMetadataName` | `AttributeDisplayName` | `ReturnTypeDisplayName` | `ParameterTypes` |
|---|---|---|---|
| `Microsoft.Agents.Extensions.Teams.App.TaskModules.FetchRouteAttribute` | `FetchRoute` | `Task<Microsoft.Teams.Api.TaskModules.Response>` | `[ITurnContext, ITurnState, Microsoft.Teams.Api.TaskModules.Request, CancellationToken]` |
| `Microsoft.Agents.Extensions.Teams.App.TaskModules.SubmitRouteAttribute` | `SubmitRoute` | `Task<Microsoft.Teams.Api.TaskModules.Response>` | `[ITurnContext, ITurnState, Microsoft.Teams.Api.TaskModules.Request, CancellationToken]` |

Use the shared metadata names from the table above for the parameter type strings.

#### TeamsChannels (9)

All nine attributes share one `SignatureRule` shape. `ReturnTypeGenericArgument` = `null` (plain `Task`). `ReturnTypeDisplayName` = `"Task"`.

`ParameterTypes` = `[Microsoft.Agents.Builder.ITurnContext, Microsoft.Agents.Builder.State.ITurnState, Microsoft.Teams.Api.Channel, System.Threading.CancellationToken]`

| `AttributeMetadataName` | `AttributeDisplayName` |
|---|---|
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelCreatedRouteAttribute` | `ChannelCreatedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelDeletedRouteAttribute` | `ChannelDeletedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelMemberAddedRouteAttribute` | `ChannelMemberAddedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelMemberRemovedRouteAttribute` | `ChannelMemberRemovedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelRenamedRouteAttribute` | `ChannelRenamedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelRestoredRouteAttribute` | `ChannelRestoredRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelSharedRouteAttribute` | `ChannelSharedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelUnSharedRouteAttribute` | `ChannelUnSharedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelUpdateRouteAttribute` | `ChannelUpdateRoute` |

#### TeamsTeams (7)

All seven attributes share one `SignatureRule` shape. `ReturnTypeGenericArgument` = `null` (plain `Task`). `ReturnTypeDisplayName` = `"Task"`.

`ParameterTypes` = `[Microsoft.Agents.Builder.ITurnContext, Microsoft.Agents.Builder.State.ITurnState, Microsoft.Teams.Api.Team, System.Threading.CancellationToken]`

| `AttributeMetadataName` | `AttributeDisplayName` |
|---|---|
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamArchivedRouteAttribute` | `TeamArchivedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamUnarchivedRouteAttribute` | `TeamUnarchivedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamDeletedRouteAttribute` | `TeamDeletedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamHardDeletedRouteAttribute` | `TeamHardDeletedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamRenamedRouteAttribute` | `TeamRenamedRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamRestoredRouteAttribute` | `TeamRestoredRoute` |
| `Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamUpdateRouteAttribute` | `TeamUpdateRoute` |

Note: `TeamUpdateRouteAttribute` is the catch-all (handles any team event), analogous to `ChannelUpdateRouteAttribute`. It shares the same signature as the six specific variants and is included in the table above as the 7th entry.

---

### Mutual Exclusivity Check

#### Naming disambiguation

The five affected attributes are all in the `MessageExtensions` namespace. They must not be confused with the `TaskModules` attributes:

| Mutual-exclusivity attribute (MessageExtensions) | Unrelated attribute (TaskModules) |
|---|---|
| `MessageExtensions.FetchTaskRouteAttribute` | `TaskModules.FetchRouteAttribute` — takes `verb`, no commandId params |
| `MessageExtensions.SubmitActionRouteAttribute` | `TaskModules.SubmitRouteAttribute` — takes `verb`, no commandId params |

`TaskModules.FetchRouteAttribute` and `TaskModules.SubmitRouteAttribute` are **not** in the mutual exclusivity set.

#### Applicable attributes and their constructor signatures

All five attributes are in `Microsoft.Agents.Extensions.Teams.App.MessageExtensions`. They use primary constructor syntax with `commandId` at index 0 and `commandIdPattern` at index 1. Both parameters are `string` with default `null`. They are constructor parameters only — there are no settable properties that could introduce `NamedArguments` for these fields, so `AttributeData.ConstructorArguments` is the only source.

```
QueryRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = ..., string signInHandlers = null)
FetchTaskRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = ..., string signInHandlers = null)
MessagePreviewEditRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = ..., string signInHandlers = null)
MessagePreviewSendRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = ..., string signInHandlers = null)
SubmitActionRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = ..., string signInHandlers = null)
```

Named-argument syntax like `[QueryRoute(commandIdPattern: "pat")]` still populates `ConstructorArguments` (not `NamedArguments`) because these are constructor parameters, not property setters. Roslyn expands all optional parameters to their defaults, so `ConstructorArguments` always has at least 5 elements.

#### Implementation

Add an `ImmutableHashSet<string>` constant `MutualExclusivityAttributeNames` containing the five metadata names:

```
Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRouteAttribute
Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRouteAttribute
Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewEditRouteAttribute
Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRouteAttribute
Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRouteAttribute
```

In `Initialize`, build a second `Dictionary<INamedTypeSymbol, string>` called `attrToDisplayName` (symbol → `AttributeDisplayName` string) for the five attributes, resolved from `MutualExclusivityAttributeNames` against the compilation. The early-exit guard changes from `if (attrToRule.Count == 0) return` to `if (attrToRule.Count == 0 && attrToDisplayName.Count == 0) return`.

Update `AnalyzeMethod` to accept both dictionaries: change the method signature to `AnalyzeMethod(SymbolAnalysisContext ctx, Dictionary<INamedTypeSymbol, SignatureRule> attrToRule, Dictionary<INamedTypeSymbol, string> attrToDisplayName)` and update the `RegisterSymbolAction` lambda accordingly: `ctx => AnalyzeMethod(ctx, attrToRule, attrToDisplayName)`.

In `AnalyzeMethod`, after the existing signature check loop, add a second loop over `method.GetAttributes()`:

```
for each attribute on the method:
  if attrToDisplayName.TryGetValue(attribute.AttributeClass, out displayName):
    args = attribute.ConstructorArguments
    if args.Length >= 2:
      commandId        = args[0].Value as string
      commandIdPattern = args[1].Value as string
      if !string.IsNullOrWhiteSpace(commandId) && !string.IsNullOrWhiteSpace(commandIdPattern):
        ReportDiagnostic(MutualExclusivityDescriptor, location, method.Name, displayName)
```

Default `null` values appear in `ConstructorArguments` as `TypedConstant` with `Value == null`, so `as string` returns `null` and the `IsNullOrWhiteSpace` guard safely passes over them.

MTEAMS004 is reported independently of MTEAMS001/002/003. If a method has both a wrong signature and a mutual-exclusivity violation, both diagnostics fire. There is no suppression relationship between them.

---

## File Changes

| File | Change |
|------|--------|
| `TeamsRouteAttributeAnalyzer.cs` | Add `MutualExclusivityDiagnosticId` constant + `MutualExclusivityDescriptor`; update `SupportedDiagnostics`; add 18 `SignatureRule` entries; add `MutualExclusivityAttributeNames` set; extend `Initialize` to build `attrToDisplayName` dictionary; add second attribute loop in `AnalyzeMethod` |
| `AnalyzerReleases.Unshipped.md` | Append `MTEAMS004 \| Usage \| Error \| TeamsRouteAttributeAnalyzer` row |
| `TeamsRouteAttributeAnalyzerTests.cs` | Add 16 new test cases |

---

## Tests

All tests follow the inline-source-string pattern. Correct-signature tests assert `Assert.Empty(diagnostics)`. Error tests assert `Assert.Single(diagnostics)` and `Assert.Equal(<DiagnosticId>, d.Id)` plus `Assert.Contains` on relevant method/attribute names.

### FetchRoute (4 tests)

- `FetchRoute_CorrectSignature_NoDiagnostic` — `Task<Microsoft.Teams.Api.TaskModules.Response>` with `(ITurnContext, ITurnState, Microsoft.Teams.Api.TaskModules.Request, CancellationToken)`
- `FetchRoute_WrongReturnType_EmitsMTEAMS001` — return `Task` instead of `Task<Response>`; assert MTEAMS001, message contains `"FetchRoute"` and `"Task<Microsoft.Teams.Api.TaskModules.Response>"`
- `FetchRoute_WrongParameterCount_EmitsMTEAMS002` — omit `Request` parameter; assert MTEAMS002, message contains `"4"`
- `FetchRoute_WrongParameterType_EmitsMTEAMS003` — replace `Request` with `string`; assert MTEAMS003, message contains `"Microsoft.Teams.Api.TaskModules.Request"`

### SubmitRoute (2 tests)

`SubmitRouteAttribute` has an identical signature rule to `FetchRouteAttribute` (same `ParameterTypes`, same `ReturnTypeGenericArgument`). Full coverage (MTEAMS002, MTEAMS003) is provided by the `FetchRoute` group; only a correct-signature smoke test and a return-type check are required here.

- `SubmitRoute_CorrectSignature_NoDiagnostic`
- `SubmitRoute_WrongReturnType_EmitsMTEAMS001`

### ChannelCreatedRoute — representative for all 9 channel variants (3 tests)

All nine `Channel*RouteAttribute` attributes resolve to the same `SignatureRule` entry (identical `ParameterTypes` and `ReturnTypeGenericArgument`). Testing one representative is sufficient; the other eight are covered by the rule-table entries themselves. Test the rule with `ChannelCreatedRouteAttribute`.

- `ChannelCreatedRoute_CorrectSignature_NoDiagnostic` — `Task` with `(ITurnContext, ITurnState, Microsoft.Teams.Api.Channel, CancellationToken)`
- `ChannelCreatedRoute_WrongReturnType_EmitsMTEAMS001` — return `Task<int>`; assert MTEAMS001, message contains `"ChannelCreatedRoute"`; assert message contains `"Task"` and does NOT contain `"Task<"` (the expected return type is plain `Task`, not `Task<T>`)
- `ChannelCreatedRoute_WrongParameterType_EmitsMTEAMS003` — replace `Channel` with `string`; assert MTEAMS003, message contains `"Microsoft.Teams.Api.Channel"`

### TeamArchivedRoute — representative for all 7 team variants (3 tests)

Same reasoning as channel variants.

- `TeamArchivedRoute_CorrectSignature_NoDiagnostic` — `Task` with `(ITurnContext, ITurnState, Microsoft.Teams.Api.Team, CancellationToken)`
- `TeamArchivedRoute_WrongReturnType_EmitsMTEAMS001` — return `Task<int>`; assert MTEAMS001, message contains `"TeamArchivedRoute"`; assert message contains `"Task"` and does NOT contain `"Task<"` (the expected return type is plain `Task`, not `Task<T>`)
- `TeamArchivedRoute_WrongParameterType_EmitsMTEAMS003` — replace `Team` with `string`; assert MTEAMS003, message contains `"Microsoft.Teams.Api.Team"`

### Mutual exclusivity (4 tests)

- `QueryRoute_BothCommandIdAndPattern_EmitsMTEAMS004` — `[QueryRoute("cmd", commandIdPattern: ".*")]`; assert MTEAMS004, message contains `"QueryRoute"`, `"commandId"`, `"commandIdPattern"`
- `QueryRoute_OnlyCommandId_NoDiagnostic` — `[QueryRoute("cmd")]`; assert empty
- `QueryRoute_OnlyCommandIdPattern_NoDiagnostic` — `[QueryRoute(commandIdPattern: "cmd.*")]`; assert empty
- `FetchTaskRoute_BothCommandIdAndPattern_EmitsMTEAMS004` — `[FetchTaskRoute("cmd", commandIdPattern: ".*")]`; assert MTEAMS004

---

## Acceptance Criteria

- All 16 new tests pass with `dotnet test`.
- No regressions in the existing 14-attribute test suite.
- `dotnet build` on the analyzer project produces no warnings or errors.
- `AnalyzerReleases.Unshipped.md` compiles without Roslyn analyzer tooling errors.
