using System.Text.RegularExpressions;

namespace Microsoft.Agents.ApiCompat;

public sealed record ParsedDiagnostic(
    string Id,
    string Target,
    string Detail,
    string? TargetFramework,
    ApiDifferenceDirection Direction);

public static partial class ApiCompatParser
{
    private const string BreakingChangesSummary =
        "API breaking changes found. If those are intentional, the APICompat suppression file can be updated by specifying the '--generate-suppression-file' parameter.";

    public static IReadOnlyList<ParsedDiagnostic> Parse(ApiCompatExecution execution, bool strict)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var diagnostics = new List<ParsedDiagnostic>();
        var seen = new HashSet<(string Id, string Target, ApiDifferenceDirection Direction, string? TargetFramework)>();
        var unparseableDiagnostics = new List<string>();
        var reportedBreakingChanges = false;

        foreach (var line in EnumerateLines(execution.StandardOutput, execution.StandardError))
        {
            if (string.Equals(line, BreakingChangesSummary, StringComparison.Ordinal))
            {
                reportedBreakingChanges = true;
                continue;
            }

            var diagnosticMatch = DiagnosticRegex().Match(line);
            if (!diagnosticMatch.Success)
            {
                if (DiagnosticPrefixRegex().IsMatch(line))
                {
                    unparseableDiagnostics.Add(line);
                }

                continue;
            }

            var id = diagnosticMatch.Groups["id"].Value;
            var detail = diagnosticMatch.Groups["detail"].Value.Trim();
            var isCandidateAddition = IsCandidateAddition(id, detail);
            if (isCandidateAddition && !strict)
            {
                throw new InvalidDataException(
                    $"ApiCompat emitted candidate-only diagnostics during a non-strict parse: {line}");
            }

            if (!TryDetermineDirection(id, detail, strict, out var direction) ||
                !IsSupportedDiagnostic(id, direction) ||
                !MatchesExpectedDetail(id, detail) ||
                !TryExtractTarget(id, detail, out var target))
            {
                unparseableDiagnostics.Add(line);
                continue;
            }

            var targetFramework = ExtractTargetFramework(detail);
            var identity = (id, target, direction, targetFramework);
            if (seen.Add(identity))
            {
                diagnostics.Add(new(id, target, detail, targetFramework, direction));
            }
        }

        if (unparseableDiagnostics.Count > 0)
        {
            throw new InvalidDataException(
                $"ApiCompat produced unparseable diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, unparseableDiagnostics)}");
        }

        if (reportedBreakingChanges && diagnostics.Count == 0)
        {
            throw new InvalidDataException("ApiCompat reported breaking changes without parseable diagnostics.");
        }

        if (execution.ExitCode != 0 && diagnostics.Count == 0)
        {
            throw new InvalidDataException(
                $"ApiCompat exited with {execution.ExitCode} without parseable diagnostics: {execution.StandardError}");
        }

        return diagnostics;
    }

    private static IEnumerable<string> EnumerateLines(params string[] outputs)
    {
        foreach (var output in outputs)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                continue;
            }

            using var reader = new StringReader(output);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line.Trim();
                }
            }
        }
    }

    private static string? ExtractTargetFramework(string detail)
    {
        var match = AssetTargetFrameworkRegex().Match(detail);
        if (match.Success)
        {
            return match.Groups["targetFramework"].Value;
        }

        match = PackageTargetFrameworkAndRidRegex().Match(detail);
        if (match.Success)
        {
            return match.Groups["targetFramework"].Value;
        }

        match = PackageTargetFrameworkRegex().Match(detail);
        return match.Success ? match.Groups["targetFramework"].Value : null;
    }

    private static bool TryDetermineDirection(string id, string detail, bool strict, out ApiDifferenceDirection direction)
    {
        if (IsCandidateAddition(id, detail))
        {
            direction = ApiDifferenceDirection.CandidateAddition;
            return strict;
        }

        if (string.Equals(id, "CP0020", StringComparison.Ordinal))
        {
            direction = default;
            return false;
        }

        direction = ApiDifferenceDirection.BaselineToCandidate;
        return true;
    }

    private static bool IsCandidateAddition(string id, string detail) =>
        string.Equals(id, "CP0020", StringComparison.Ordinal)
            ? PublicVisibilityExpansionDetailRegex().IsMatch(detail)
            : CandidateAdditionRegex().IsMatch(detail);

    private static bool IsSupportedDiagnostic(string id, ApiDifferenceDirection direction)
    {
        try
        {
            _ = DiagnosticClassifier.Classify(id, direction);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool MatchesExpectedDetail(string id, string detail) =>
        id switch
        {
            "CP0001" => MissingTypeDetailRegex().IsMatch(detail),
            "CP0002" => MissingMemberDetailRegex().IsMatch(detail),
            "CP0003" => AssemblyNameMismatchDetailRegex().IsMatch(detail) ||
                AssemblyCultureMismatchDetailRegex().IsMatch(detail) ||
                AssemblyPublicKeyTokenMismatchDetailRegex().IsMatch(detail) ||
                AssemblyVersionEqualDetailRegex().IsMatch(detail) ||
                AssemblyVersionCompatibleDetailRegex().IsMatch(detail),
            "CP0004" => MissingAssemblyDetailRegex().IsMatch(detail),
            "CP0005" => CannotAddAbstractMemberDetailRegex().IsMatch(detail),
            "CP0006" => CannotAddInterfaceMemberDetailRegex().IsMatch(detail),
            "CP0007" => MissingBaseTypeDetailRegex().IsMatch(detail),
            "CP0008" => MissingBaseInterfaceDetailRegex().IsMatch(detail),
            "CP0009" => SealedTypeDetailRegex().IsMatch(detail) ||
                EffectivelySealedTypeDetailRegex().IsMatch(detail),
            "CP0010" => EnumUnderlyingTypeDetailRegex().IsMatch(detail),
            "CP0012" => RemovedVirtualOrAbstractDetailRegex().IsMatch(detail),
            "CP0017" => ParameterNameChangedDetailRegex().IsMatch(detail),
            "CP0018" => SealedInterfaceMemberDetailRegex().IsMatch(detail),
            "CP0019" => ReducedVisibilityDetailRegex().IsMatch(detail),
            "CP0020" => PublicVisibilityExpansionDetailRegex().IsMatch(detail),
            "PKV001" => MissingCompileTimeAssetDetailRegex().IsMatch(detail),
            "PKV002" => MissingRuntimeAssetDetailRegex().IsMatch(detail),
            "PKV003" => MissingRuntimeAssetDetailRegex().IsMatch(detail),
            "PKV004" => MissingRuntimeAssetDetailRegex().IsMatch(detail),
            "PKV005" => MissingRuntimeAssetDetailRegex().IsMatch(detail),
            "PKV006" => MissingTargetFrameworkDetailRegex().IsMatch(detail),
            "PKV007" => MissingTargetFrameworkAndRidDetailRegex().IsMatch(detail),
            _ => false,
        };

    private static bool TryExtractTarget(string id, string detail, out string target)
    {
        var match = QuotedTargetRegex().Match(detail);
        if (match.Success)
        {
            target = match.Groups["target"].Value;
            return true;
        }

        if (AssemblyIdentityFacetRegex().Match(detail) is { Success: true } assemblyFacetMatch)
        {
            target = $"assembly {assemblyFacetMatch.Groups["facet"].Value}";
            return true;
        }

        if (PackageTargetFrameworkAndRidRegex().Match(detail) is { Success: true } packageRidMatch)
        {
            target = $"{packageRidMatch.Groups["targetFramework"].Value}::{packageRidMatch.Groups["rid"].Value}";
            return true;
        }

        if (PackageTargetFrameworkRegex().Match(detail) is { Success: true } packageTargetMatch)
        {
            target = packageTargetMatch.Groups["targetFramework"].Value;
            return true;
        }

        target = string.Empty;
        return false;
    }

    [GeneratedRegex(@"^(?<id>CP\d{4}|PKV\d{3}): (?<detail>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticRegex();

    [GeneratedRegex(@"^(CP\d{4}|PKV\d{3}): ", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticPrefixRegex();

    [GeneratedRegex(@"(?:^|\b)(?:member|type|enum|field|visibility of|assembly with name|assembly name) '(?<target>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuotedTargetRegex();

    [GeneratedRegex(@"\bassembly (?<facet>culture|public key token|version)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyIdentityFacetRegex();

    [GeneratedRegex(@"^Target framework (?<targetFramework>.+?) and runtime identifier \(RID\) (?<rid>.+?) is no longer supported in the latest version\.$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageTargetFrameworkAndRidRegex();

    [GeneratedRegex(@"^Target framework (?<targetFramework>.+?)(?: does not have a compatible compile time asset in the package\.| does not have a compatible runtime asset in the package\.| is no longer supported in the latest version\.)$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageTargetFrameworkRegex();

    [GeneratedRegex(@"^(?:Type|Member) '[^']+' exists on (?!\[Baseline\])\S+ but not on \[Baseline\] \S+$|^Cannot add (?:abstract member|interface member) '[^']+' to (?!\[Baseline\])\S+ because it does not exist on \[Baseline\] \S+$", RegexOptions.CultureInvariant)]
    private static partial Regex CandidateAdditionRegex();

    [GeneratedRegex(@"^Type '[^']+' exists on (?:\[Baseline\] )?\S+ but not on (?:\[Baseline\] )?\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingTypeDetailRegex();

    [GeneratedRegex(@"^Member '[^']+' exists on (?:\[Baseline\] )?\S+ but not on (?:\[Baseline\] )?\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingMemberDetailRegex();

    [GeneratedRegex(@"^.+ assembly name '[^']+' does not match with .+ assembly name '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyNameMismatchDetailRegex();

    [GeneratedRegex(@"^.+ assembly culture '[^']+' does not match with .+ assembly culture '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyCultureMismatchDetailRegex();

    [GeneratedRegex(@"^.+ assembly public key token '[^']+' does not match with .+ '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyPublicKeyTokenMismatchDetailRegex();

    [GeneratedRegex(@"^.+ assembly version '[^']+' should be equal to .+ version '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyVersionEqualDetailRegex();

    [GeneratedRegex(@"^.+ assembly version '[^']+' should be equal to or higher than .+ version '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyVersionCompatibleDetailRegex();

    [GeneratedRegex(@"^Assembly with name '[^']+' does not exist at .+\.$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingAssemblyDetailRegex();

    [GeneratedRegex(@"^Cannot add abstract member '[^']+' to .+ because it does not exist on .+$", RegexOptions.CultureInvariant)]
    private static partial Regex CannotAddAbstractMemberDetailRegex();

    [GeneratedRegex(@"^Cannot add interface member '[^']+' to .+ because it does not exist on .+$", RegexOptions.CultureInvariant)]
    private static partial Regex CannotAddInterfaceMemberDetailRegex();

    [GeneratedRegex(@"^Type '[^']+' does not inherit from base type '[^']+' on .+ but it does on .+$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingBaseTypeDetailRegex();

    [GeneratedRegex(@"^Type '[^']+' does not implement interface '[^']+' on .+ but it does on .+$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingBaseInterfaceDetailRegex();

    [GeneratedRegex(@"^Type '[^']+' has the sealed modifier on .+ but not on .+$", RegexOptions.CultureInvariant)]
    private static partial Regex SealedTypeDetailRegex();

    [GeneratedRegex(@"^Type '[^']+' is sealed because it has no visible constructor on .+ but it does on .+$", RegexOptions.CultureInvariant)]
    private static partial Regex EffectivelySealedTypeDetailRegex();

    [GeneratedRegex(@"^Underlying type of enum '[^']+' changed from '[^']+' to '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex EnumUnderlyingTypeDetailRegex();

    [GeneratedRegex(@"^Cannot remove '[^']+' keyword from member '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex RemovedVirtualOrAbstractDetailRegex();

    [GeneratedRegex(@"^Parameter name on member '[^']+' changed from '[^']+' to '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterNameChangedDetailRegex();

    [GeneratedRegex(@"^Cannot add sealed keyword to default interface member '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex SealedInterfaceMemberDetailRegex();

    [GeneratedRegex(@"^Visibility of '[^']+' reduced from '[^']+' to '[^']+'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ReducedVisibilityDetailRegex();

    [GeneratedRegex(@"^Visibility of '[^']+' expanded from 'Protected' to 'Public'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex PublicVisibilityExpansionDetailRegex();

    [GeneratedRegex(@"^Target framework .+ does not have a compatible compile time asset in the package\.$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingCompileTimeAssetDetailRegex();

    [GeneratedRegex(@"^Target framework .+ does not have a compatible runtime asset in the package\.$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingRuntimeAssetDetailRegex();

    [GeneratedRegex(@"^Target framework .+ is no longer supported in the latest version\.$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingTargetFrameworkDetailRegex();

    [GeneratedRegex(@"^Target framework .+ and runtime identifier \(RID\) .+ is no longer supported in the latest version\.$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingTargetFrameworkAndRidDetailRegex();

    [GeneratedRegex(@"lib/(?<targetFramework>[^/]+)/[^ ]+", RegexOptions.CultureInvariant)]
    private static partial Regex AssetTargetFrameworkRegex();
}
