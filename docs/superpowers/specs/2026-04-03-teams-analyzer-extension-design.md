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

## Design

### Approach

Extend `TeamsRouteAttributeAnalyzer.cs` directly (Approach A). All changes are additive to the existing single-file analyzer.

### New Diagnostic: MTEAMS004

```
ID:       MTEAMS004
Severity: Error
Category: Usage
Title:    Mutually exclusive attribute arguments
Message:  Method '{0}' decorated with '[{1}]' cannot specify both 'commandId'
          and 'commandIdPattern' — they are mutually exclusive
```

Reported at the method's first source location (same convention as MTEAMS001–003).

`AnalyzerReleases.Unshipped.md` updated with the new rule entry.

### New Signature Rules (18 entries)

Added to the existing `Rules` `ImmutableArray<SignatureRule>`:

#### TaskModules (2)

Both attributes share the same signature:

| Attribute | Return type | Parameters |
|-----------|-------------|------------|
| `TaskModules.FetchRouteAttribute` | `Task<Microsoft.Teams.Api.TaskModules.Response>` | `ITurnContext, ITurnState, Microsoft.Teams.Api.TaskModules.Request, CancellationToken` |
| `TaskModules.SubmitRouteAttribute` | `Task<Microsoft.Teams.Api.TaskModules.Response>` | `ITurnContext, ITurnState, Microsoft.Teams.Api.TaskModules.Request, CancellationToken` |

#### TeamsChannels (9)

All nine attributes share the same signature (plain `Task`):

`ChannelCreatedRoute`, `ChannelDeletedRoute`, `ChannelMemberAddedRoute`, `ChannelMemberRemovedRoute`, `ChannelRenamedRoute`, `ChannelRestoredRoute`, `ChannelSharedRoute`, `ChannelUnSharedRoute`, `ChannelUpdateRoute`

| Return type | Parameters |
|-------------|------------|
| `Task` | `ITurnContext, ITurnState, Microsoft.Teams.Api.Channel, CancellationToken` |

#### TeamsTeams (7)

All seven attributes share the same signature (plain `Task`):

`TeamArchivedRoute`, `TeamUnarchivedRoute`, `TeamDeletedRoute`, `TeamHardDeletedRoute`, `TeamRenamedRoute`, `TeamRestoredRoute`, `TeamUpdateRoute`

| Return type | Parameters |
|-------------|------------|
| `Task` | `ITurnContext, ITurnState, Microsoft.Teams.Api.Team, CancellationToken` |

### Mutual Exclusivity Check

A new `ImmutableHashSet<string>` (`MutualExclusivityAttributeNames`) lists the five attribute metadata names that require the check:

- `Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRouteAttribute`
- `Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRouteAttribute`
- `Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewEditRouteAttribute`
- `Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRouteAttribute`
- `Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRouteAttribute`

At compilation start, these are resolved to `INamedTypeSymbol` keys and stored in a second dictionary alongside the existing `attrToRule` dictionary.

In `AnalyzeMethod`, after the existing three signature checks, for attributes present in the mutual exclusivity set:

```
args = attribute.ConstructorArguments
commandId      = args[0]   (string, index 0 in all five constructors)
commandIdPattern = args[1] (string, index 1 in all five constructors)

if both are non-null, non-empty strings → emit MTEAMS004
```

`AttributeData.ConstructorArguments` returns all parameters in declaration order with default values for omitted optionals, making positional index access reliable regardless of whether the caller used named-argument syntax.

---

## File Changes

| File | Change |
|------|--------|
| `TeamsRouteAttributeAnalyzer.cs` | Add `MutualExclusivityDescriptor` + `MTEAMS004` constant; add 18 `SignatureRule` entries; add `MutualExclusivityAttributeNames` set; extend `Initialize` to build mutual-exclusivity symbol map; extend `AnalyzeMethod` with mutual-exclusivity check |
| `AnalyzerReleases.Unshipped.md` | Add MTEAMS004 row |
| `TeamsRouteAttributeAnalyzerTests.cs` | Add ~15 new test cases (see below) |

---

## Tests

All tests follow the existing inline-source-string pattern in `TeamsRouteAttributeAnalyzerTests.cs`.

### FetchRoute (4 tests)
- `FetchRoute_CorrectSignature_NoDiagnostic`
- `FetchRoute_WrongReturnType_EmitsMTEAMS001`
- `FetchRoute_WrongParameterCount_EmitsMTEAMS002`
- `FetchRoute_WrongParameterType_EmitsMTEAMS003`

### SubmitRoute (2 tests)
- `SubmitRoute_CorrectSignature_NoDiagnostic`
- `SubmitRoute_WrongReturnType_EmitsMTEAMS001`

### ChannelCreatedRoute — representative for all channel variants (3 tests)
- `ChannelCreatedRoute_CorrectSignature_NoDiagnostic`
- `ChannelCreatedRoute_WrongReturnType_EmitsMTEAMS001`
- `ChannelCreatedRoute_WrongParameterType_EmitsMTEAMS003`

### TeamArchivedRoute — representative for all team variants (3 tests)
- `TeamArchivedRoute_CorrectSignature_NoDiagnostic`
- `TeamArchivedRoute_WrongReturnType_EmitsMTEAMS001`
- `TeamArchivedRoute_WrongParameterType_EmitsMTEAMS003`

### Mutual exclusivity (4 tests)
- `QueryRoute_BothCommandIdAndPattern_EmitsMTEAMS004`
- `QueryRoute_OnlyCommandId_NoDiagnostic`
- `QueryRoute_OnlyCommandIdPattern_NoDiagnostic`
- `FetchTaskRoute_BothCommandIdAndPattern_EmitsMTEAMS004`

---

## Acceptance Criteria

- All new tests pass with `dotnet test`.
- No regressions in the existing 14-attribute test suite.
- `dotnet build` on the analyzer project produces no warnings.
