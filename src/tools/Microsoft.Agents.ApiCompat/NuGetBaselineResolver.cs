using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.ApiCompat;

public sealed partial class NuGetBaselineResolver(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<string?> GetBaselineVersionAsync(
        string packageId,
        string baseRef,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);

        var normalizedPackageId = packageId.ToLowerInvariant();
        using var response = await _httpClient.GetAsync(
            $"https://api.nuget.org/v3-flatcontainer/{normalizedPackageId}/index.json",
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var versions = document.RootElement.GetProperty("versions")
            .EnumerateArray()
            .Select(version => version.GetString())
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>();

        return SelectBaseline(baseRef, versions);
    }

    public async Task DownloadAsync(
        string packageId,
        string version,
        string destination,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var normalizedPackageId = packageId.ToLowerInvariant();
        var normalizedVersion = version.ToLowerInvariant();
        using var response = await _httpClient.GetAsync(
            $"https://api.nuget.org/v3-flatcontainer/{normalizedPackageId}/{normalizedVersion}/{normalizedPackageId}.{normalizedVersion}.nupkg",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var destinationDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await using var output = File.Create(destination);
        await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    public static string? SelectBaseline(string baseRef, IEnumerable<string> versions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        ArgumentNullException.ThrowIfNull(versions);

        var branchMatch = ReleaseBranchRegex().Match(baseRef);
        var branchMajor = branchMatch.Success
            ? int.Parse(branchMatch.Groups["major"].Value, CultureInfo.InvariantCulture)
            : (int?)null;
        var branchMinor = branchMatch.Success
            ? int.Parse(branchMatch.Groups["minor"].Value, CultureInfo.InvariantCulture)
            : (int?)null;

        StableVersionCandidate? selected = null;
        foreach (var version in versions)
        {
            if (!StableNuGetVersion.TryParse(version, out var parsedVersion))
            {
                continue;
            }

            if (branchMajor is not null && parsedVersion.Major != branchMajor.Value)
            {
                continue;
            }

            if (branchMinor is not null && parsedVersion.Minor != branchMinor.Value)
            {
                continue;
            }

            var candidate = new StableVersionCandidate(version, parsedVersion);
            if (selected is null || candidate.Version.CompareTo(selected.Version) > 0)
            {
                selected = candidate;
            }
        }

        return selected?.Text;
    }

    [GeneratedRegex(@"^rel/v(?<major>\d+)\.(?<minor>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseBranchRegex();

    private sealed record StableVersionCandidate(string Text, StableNuGetVersion Version);

    private sealed class StableNuGetVersion : IComparable<StableNuGetVersion>
    {
        private readonly int[] _segments;

        public StableNuGetVersion(int[] segments)
        {
            _segments = segments;
        }

        public int Major => GetSegment(0);

        public int Minor => GetSegment(1);

        public int CompareTo(StableNuGetVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            var count = Math.Max(_segments.Length, other._segments.Length);
            for (var index = 0; index < count; index++)
            {
                var comparison = GetSegment(index).CompareTo(other.GetSegment(index));
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }
        public static bool TryParse(string? text, out StableNuGetVersion version)
        {
            version = null!;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var releasePart = text.Split('+', 2)[0];
            if (releasePart.Contains('-', StringComparison.Ordinal))
            {
                return false;
            }

            var tokens = releasePart.Split('.', StringSplitOptions.None);
            if (tokens.Length == 0)
            {
                return false;
            }

            var parsedSegments = new int[tokens.Length];
            for (var index = 0; index < tokens.Length; index++)
            {
                if (!int.TryParse(tokens[index], NumberStyles.None, CultureInfo.InvariantCulture, out parsedSegments[index]))
                {
                    return false;
                }
            }

            version = new StableNuGetVersion(parsedSegments);
            return true;
        }

        private int GetSegment(int index)
        {
            return index < _segments.Length ? _segments[index] : 0;
        }
    }
}
