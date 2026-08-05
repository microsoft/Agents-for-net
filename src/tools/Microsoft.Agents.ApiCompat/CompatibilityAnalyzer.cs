using System.IO.Compression;
using System.Xml.Linq;

namespace Microsoft.Agents.ApiCompat;

public sealed record AnalysisOptions(
    string RepositoryRoot,
    string CandidatePackageDirectory,
    string OutputDirectory,
    PullRequestEvent Event);

public sealed class CompatibilityAnalyzer(NuGetBaselineResolver resolver)
{
    private readonly NuGetBaselineResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public async Task<CompatibilityReport> AnalyzeAsync(AnalysisOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var packages = new List<PackageCompatibilityReport>();
        var infrastructureErrors = new List<string>();

        var workingDirectory = Directory.CreateTempSubdirectory("apicompat-baseline-");
        try
        {
            foreach (var project in ProjectDiscovery.Discover(options.RepositoryRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var package = await AnalyzePackageAsync(
                        project,
                        options,
                        workingDirectory.FullName,
                        infrastructureErrors,
                        cancellationToken).ConfigureAwait(false);
                    if (package is not null)
                    {
                        packages.Add(package);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    infrastructureErrors.Add(
                        $"Analysis of '{project.PackageId}' failed: {exception.Message}");
                }
            }
        }
        finally
        {
            TryDelete(workingDirectory.FullName);
        }

        var overrideResult = OverridePolicy.Evaluate(options.Event.Labels, options.Event.Body);
        var blockingCount = packages
            .SelectMany(package => package.Findings)
            .Count(finding => finding.Severity == FindingSeverity.Blocking);
        var decision = Decide(blockingCount, overrideResult.IsValid, infrastructureErrors);

        return new CompatibilityReport(
            SchemaVersion: 1,
            RunId: options.Event.RunId,
            PullRequestNumber: options.Event.Number,
            BaseRef: options.Event.BaseRef,
            Decision: decision,
            Override: overrideResult,
            Packages: packages,
            InfrastructureErrors: infrastructureErrors);
    }

    public static AnalysisDecision Decide(int blockingCount, bool overrideValid, IReadOnlyCollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count > 0)
        {
            return AnalysisDecision.InfrastructureFailure;
        }

        if (blockingCount == 0)
        {
            return AnalysisDecision.Pass;
        }

        return overrideValid ? AnalysisDecision.Overridden : AnalysisDecision.Block;
    }

    public static string ReadPackageVersion(string nupkgPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nupkgPath);

        using var archive = ZipFile.OpenRead(nupkgPath);
        var nuspecEntry = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
            !entry.FullName.Contains('/', StringComparison.Ordinal));
        if (nuspecEntry is null)
        {
            throw new InvalidDataException($"Package '{nupkgPath}' does not contain a .nuspec manifest.");
        }

        using var stream = nuspecEntry.Open();
        var document = XDocument.Load(stream);
        var version = document.Root?
            .Elements().FirstOrDefault(element => element.Name.LocalName.Equals("metadata", StringComparison.Ordinal))?
            .Elements().FirstOrDefault(element => element.Name.LocalName.Equals("version", StringComparison.Ordinal))?
            .Value.Trim();

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException($"Package '{nupkgPath}' does not declare a version.");
        }

        return version;
    }

    private async Task<PackageCompatibilityReport?> AnalyzePackageAsync(
        PackageProject project,
        AnalysisOptions options,
        string workingDirectory,
        List<string> infrastructureErrors,
        CancellationToken cancellationToken)
    {
        var matches = Directory.GetFiles(options.CandidatePackageDirectory, $"{project.PackageId}.*.nupkg");
        if (matches.Length != 1)
        {
            infrastructureErrors.Add(
                $"Expected one candidate package for '{project.PackageId}', found {matches.Length}.");
            return null;
        }

        var candidatePackage = matches[0];
        var candidateVersion = ReadPackageVersion(candidatePackage);

        var baselineVersion = await _resolver
            .GetBaselineVersionAsync(project.PackageId, options.Event.BaseRef, cancellationToken)
            .ConfigureAwait(false);
        if (baselineVersion is null)
        {
            return new(project.PackageId, candidateVersion, null, "NoBaseline", []);
        }

        var baselinePackage = Path.Combine(workingDirectory, $"{project.PackageId}.{baselineVersion}.nupkg");
        await _resolver
            .DownloadAsync(project.PackageId, baselineVersion, baselinePackage, cancellationToken)
            .ConfigureAwait(false);

        var normalExecution = await ApiCompatRunner
            .RunAsync(candidatePackage, baselinePackage, strict: false, cancellationToken)
            .ConfigureAwait(false);
        var strictExecution = await ApiCompatRunner
            .RunAsync(candidatePackage, baselinePackage, strict: true, cancellationToken)
            .ConfigureAwait(false);

        var diagnostics = ApiCompatParser.Parse(normalExecution, strict: false)
            .Concat(ApiCompatParser.Parse(strictExecution, strict: true));

        var findings = new List<CompatibilityFinding>();
        var seen = new HashSet<(string, string, string?, ApiDifferenceDirection)>();
        foreach (var diagnostic in diagnostics)
        {
            if (!seen.Add((diagnostic.Id, diagnostic.Target, diagnostic.TargetFramework, diagnostic.Direction)))
            {
                continue;
            }

            var classification = DiagnosticClassifier.Classify(diagnostic.Id, diagnostic.Direction);
            findings.Add(new CompatibilityFinding(
                project.PackageId,
                baselineVersion,
                candidateVersion,
                diagnostic.TargetFramework,
                diagnostic.Id,
                diagnostic.Target,
                diagnostic.Detail,
                classification.Category,
                classification.Severity));
        }

        var status = findings.Count == 0
            ? "Compatible"
            : findings.Any(finding => finding.Severity == FindingSeverity.Blocking) ? "Breaking" : "Warnings";

        return new(project.PackageId, candidateVersion, baselineVersion, status, findings);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
