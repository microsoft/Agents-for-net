namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ExperimentalApiAssessment(string DiagnosticId, string Support, string Reason);

internal static class ExperimentalApiSupport
{
    internal const string Supported = "supported";
    internal const string Unsupported = "intentionally-unsupported";

    internal static IReadOnlyList<string> ValidateScopes(CapabilityDocument capabilities)
    {
        var errors = new List<string>();
        var seen = new HashSet<(string DiagnosticId, string Symbol, string? Member)>();
        foreach (var scope in capabilities.ExperimentalApis ?? [])
        {
            if (scope is null || string.IsNullOrWhiteSpace(scope.DiagnosticId) || string.IsNullOrWhiteSpace(scope.Symbol) ||
                (scope.Member is not null && string.IsNullOrWhiteSpace(scope.Member)) ||
                scope.Support is not (Supported or Unsupported) || string.IsNullOrWhiteSpace(scope.Reason))
            {
                errors.Add("Each experimental API scope must have a diagnostic ID, exact symbol, supported or intentionally-unsupported status, and reason.");
                continue;
            }

            if (!seen.Add((scope.DiagnosticId, scope.Symbol, scope.Member)))
            {
                errors.Add($"Duplicate experimental API scope: {scope.DiagnosticId}, {scope.Symbol}, {scope.Member ?? "(type)"}.");
            }
        }

        return errors;
    }

    internal static bool HasRecordedUse(ApiChange change, UsageManifest manifest, CollectedUsage? collected) =>
        manifest.Usages.Any(use => use.UpstreamSymbol == change.Symbol &&
            (change.Member is null || use.Members.Count == 0 || use.Members.Contains(change.Member, StringComparer.Ordinal))) ||
        collected?.Usages.Any(use => use.UpstreamSymbol == change.Symbol &&
            (change.Member is null || use.Members.Contains(change.Member, StringComparer.Ordinal))) == true;

    internal static IReadOnlyDictionary<string, IReadOnlyList<ExperimentalApiAssessment>>? Assess(
        ApiComparison comparison, CapabilityDocument capabilities, UsageManifest manifest, CollectedUsage? collected)
    {
        var results = new Dictionary<string, IReadOnlyList<ExperimentalApiAssessment>>(StringComparer.Ordinal);
        foreach (var change in comparison.Changes)
        {
            if (change.BeforeExperimentalDiagnosticId is null && change.AfterExperimentalDiagnosticId is null)
            {
                continue;
            }

            var used = HasRecordedUse(change, manifest, collected);
            results.Add(change.Id, new[] { change.BeforeExperimentalDiagnosticId, change.AfterExperimentalDiagnosticId }
                .OfType<string>().Distinct(StringComparer.Ordinal)
                .Select(id => AssessDiagnostic(change, id, used, capabilities.ExperimentalApis ?? [])).ToArray());
        }

        return results.Count == 0 ? null : results;
    }

    private static ExperimentalApiAssessment AssessDiagnostic(ApiChange change, string diagnosticId, bool used,
        IReadOnlyList<ExperimentalApiScope> scopes)
    {
        var matching = scopes.Where(scope => scope.DiagnosticId == diagnosticId && scope.Symbol == change.Symbol);
        var scope = matching.FirstOrDefault(scope => scope.Member == change.Member)
            ?? matching.FirstOrDefault(scope => scope.Member is null);

        if (used)
        {
            var reason = scope?.Support switch
            {
                Unsupported => $"Recorded SDK usage conflicts with the declared exclusion. Review the existing use. Exclusion reason: {scope.Reason}",
                Supported => scope.Reason,
                _ => "The usage manifest or built extension records usage or public exposure of this API."
            };
            return new(diagnosticId, Supported, reason);
        }

        return scope is null
            ? new(diagnosticId, "unclassified", "No matching support decision or recorded SDK usage.")
            : new(diagnosticId, scope.Support, scope.Reason);
    }
}
