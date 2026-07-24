---
applyTo:
  - "**"
---

# Code review rules (Microsoft 365 Agents SDK for .NET)

These are the repository-wide rules for **GitHub Copilot code review**. Because they live in
`.github/instructions/`, GitHub Copilot applies them automatically when reviewing pull requests,
and the local `review` agent (`.github/agents/review.agent.md`) loads the same file. This is the
**single source of truth** for review rules — edit this file to change what both GitHub and the
local review enforce, so the two never diverge.

Path-specific rules for particular subsystems live in the sibling `*.instructions.md` files
(serialization, oauth-flows, cloudadapter-pipeline, proactive-messaging, streaming-response) and
apply in addition to these when a changed file matches their `applyTo` globs.

## What NOT to comment on

- Do **not** comment on style, formatting, naming, or documentation wording.
- Do **not** comment on "best practices" that do not prevent an actual problem.
- Do **not** raise a finding you cannot tie to an exact `path:line` with a concrete failure.
- If uncertain, stay silent. Only report high-confidence findings.

## Anti-false-positive checks (perform ALL before raising any finding)

1. **Check for guards before flagging complexity.** Look for size limits, depth caps, early
   returns, `CancellationToken` checks, and bounded collections. If a guard exists, drop the finding.
2. **Trace the call site, not just the method.** Identify callers and frequency: per-turn (hot)
   vs. startup/configuration (cold). State the call frequency in the finding.
3. **Understand platform constraints before suggesting alternatives.** Verify the suggestion is
   possible given Activity Protocol constraints, the `netstandard2.0` API surface, and named-pipe
   framing requirements.
4. **Check DI/HTTP-layer resilience before claiming "no retry".** Inspect service registration,
   `HttpClient` configuration, and any `IHttpClientFactory` policies.
5. **Distinguish sequential from nested parallelism.** Two awaited async calls in one method are
   not nested if the first completes before the second starts.
6. **Estimate proportional impact.** Processing a single Activity or sorting a small handler list
   is not worth flagging. Include estimated cost (ms, allocations) when raising performance findings.

## Review lenses

- **Correctness:** logic errors, edge cases, null-reference paths (nullable annotations only —
  warnings are not enforced), unchecked casts, race conditions with evidence of shared mutable
  state, incorrect async patterns (fire-and-forget, sync-over-async, deadlock risk), missing error
  handling on paths that can throw.
- **Security:** input validation, injection, data exposure, token handling, auth-scope misuse,
  credential leakage in logs/telemetry.
- **Serialization:** `System.Text.Json` correctness — missing converters, incorrect
  `JsonPropertyName` attributes, wire-format breaking changes, `ProtocolJsonSerializer` misuse.
  (See `serialization-extension.instructions.md` for details.)
- **Multi-target compatibility:** APIs used that do not exist in `netstandard2.0` (core libraries
  multi-target `net8.0` and `netstandard2.0`).
- **Performance (with proportional impact):** unbounded collections in per-turn paths (after
  confirming no caps exist), N+1 in hot paths, excessive allocations in tight loops.
- **Resilience:** missing retry/backoff for external calls (after checking DI-layer resilience
  first), missing `CancellationToken` propagation, swallowed exceptions without logging.
- **Architecture:** layer violations (Core must not depend on Builder/Hosting), Activity Protocol
  contract fidelity, `IStorage` contract compliance, named-pipe protocol correctness (48-byte ASCII
  framed headers, 64KB chunk size).
- **Build system:** Central Package Management violations (versions in a `.csproj` instead of
  `Directory.Packages.props`), `TreatWarningsAsErrors` suppressions without justification.

## Severity

Classify every finding as **Critical**, **High**, or **Medium**. Do not pad reviews with filler,
summaries, or compliments — silence is better than noise.
