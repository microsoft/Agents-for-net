using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.TeamsApiDrift;

public static class ApiComparer
{
    public static ApiComparison Compare(ApiModel before, ApiModel after)
    {
        var raw = new List<RawChange>();
        var beforeFrameworks = before.Frameworks.ToDictionary(item => item.TargetFramework, StringComparer.Ordinal);
        var afterFrameworks = after.Frameworks.ToDictionary(item => item.TargetFramework, StringComparer.Ordinal);
        foreach (var framework in beforeFrameworks.Keys.Union(afterFrameworks.Keys).Order(StringComparer.Ordinal))
        {
            beforeFrameworks.TryGetValue(framework, out var oldFramework);
            afterFrameworks.TryGetValue(framework, out var newFramework);
            if (oldFramework?.Asset is null || newFramework?.Asset is null)
            {
                if (oldFramework?.Asset != newFramework?.Asset)
                {
                    raw.Add(new RawChange(
                        oldFramework?.Asset is null ? "framework-asset-added" : "framework-asset-removed",
                        $"@framework/{framework}", null, oldFramework?.Asset, newFramework?.Asset,
                        oldFramework?.Asset is null ? "non-breaking" : "breaking", framework));
                }
                continue;
            }

            CompareFramework(oldFramework, newFramework, raw);
        }

        var changes = raw.GroupBy(change => new { change.Kind, change.Symbol, change.Member, change.Before, change.After, change.Compatibility })
            .Select(group => new
            {
                group.Key,
                Frameworks = group.Select(change => change.Framework).Distinct().Order(StringComparer.Ordinal).ToArray()
            })
            .OrderBy(item => item.Key.Symbol, StringComparer.Ordinal)
            .ThenBy(item => item.Key.Member, StringComparer.Ordinal)
            .ThenBy(item => item.Key.Kind, StringComparer.Ordinal)
            .Select((item, index) => new ApiChange(
                $"MTAPI-{index + 1:0000}", item.Key.Kind, item.Key.Symbol, item.Key.Member,
                item.Key.Before, item.Key.After, item.Key.Compatibility, item.Frameworks, ["normalized-api-model"]))
            .ToArray();
        return new ApiComparison(1, PackageConstants.PackageId, before.Version, after.Version, changes.Length > 0, changes);
    }

    private static void CompareFramework(FrameworkApiModel before, FrameworkApiModel after, List<RawChange> changes)
    {
        var oldSymbols = before.Symbols.ToDictionary(symbol => symbol.Name, StringComparer.Ordinal);
        var newSymbols = after.Symbols.ToDictionary(symbol => symbol.Name, StringComparer.Ordinal);
        foreach (var symbolName in oldSymbols.Keys.Union(newSymbols.Keys).Order(StringComparer.Ordinal))
        {
            oldSymbols.TryGetValue(symbolName, out var oldSymbol);
            newSymbols.TryGetValue(symbolName, out var newSymbol);
            if (oldSymbol is null || newSymbol is null)
            {
                changes.Add(new RawChange(oldSymbol is null ? "symbol-added" : "symbol-removed", symbolName, null,
                    oldSymbol?.Kind, newSymbol?.Kind, oldSymbol is null ? "non-breaking" : "breaking", before.TargetFramework));
                continue;
            }

            AddIfChanged(changes, "symbol-kind-changed", symbolName, null, oldSymbol.Kind, newSymbol.Kind, "breaking", before.TargetFramework);
            AddIfChanged(changes, "accessibility-changed", symbolName, null, oldSymbol.Accessibility, newSymbol.Accessibility, "potentially-breaking", before.TargetFramework);
            AddIfChanged(changes, "base-type-changed", symbolName, null, oldSymbol.BaseType, newSymbol.BaseType, "potentially-breaking", before.TargetFramework);
            AddIfChanged(changes, "interfaces-changed", symbolName, null, Join(oldSymbol.Interfaces), Join(newSymbol.Interfaces), "potentially-breaking", before.TargetFramework);
            AddIfChanged(changes, "generic-constraints-changed", symbolName, null, Join(oldSymbol.GenericConstraints), Join(newSymbol.GenericConstraints), "potentially-breaking", before.TargetFramework);
            if (oldSymbol.Obsolete != newSymbol.Obsolete)
            {
                changes.Add(new RawChange(newSymbol.Obsolete ? "deprecation-added" : "deprecation-removed", symbolName, null,
                    oldSymbol.Obsolete.ToString(), newSymbol.Obsolete.ToString(), "unknown", before.TargetFramework));
            }

            CompareMembers(symbolName, oldSymbol.Kind, oldSymbol.Members, newSymbol.Members, before.TargetFramework, changes);
        }
    }

    private static void CompareMembers(string symbol, string symbolKind, IReadOnlyList<ApiMemberModel> before, IReadOnlyList<ApiMemberModel> after, string framework, List<RawChange> changes)
    {
        var oldGroups = before.GroupBy(member => $"{member.Kind}:{member.Name}").ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var newGroups = after.GroupBy(member => $"{member.Kind}:{member.Name}").ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var groupName in oldGroups.Keys.Union(newGroups.Keys).Order(StringComparer.Ordinal))
        {
            oldGroups.TryGetValue(groupName, out var oldMembers);
            newGroups.TryGetValue(groupName, out var newMembers);
            oldMembers ??= [];
            newMembers ??= [];
            var memberName = groupName[(groupName.IndexOf(':') + 1)..];
            if (oldMembers.Length == 1 && newMembers.Length == 1 && oldMembers[0].Key != newMembers[0].Key)
            {
                var changeKind = symbolKind == "enum" && oldMembers[0].Kind == "field"
                    ? "enum-value-changed"
                    : WithoutNullability(oldMembers[0].Signature) == WithoutNullability(newMembers[0].Signature)
                        ? "nullability-changed"
                        : "member-signature-changed";
                changes.Add(new RawChange(
                    changeKind,
                    symbol, memberName, oldMembers[0].Signature, newMembers[0].Signature, "potentially-breaking", framework));
                continue;
            }
            foreach (var member in oldMembers.Where(old => newMembers.All(current => current.Key != old.Key)))
            {
                changes.Add(new RawChange("member-removed", symbol, memberName, member.Signature, null, "breaking", framework));
            }
            foreach (var member in newMembers.Where(current => oldMembers.All(old => old.Key != current.Key)))
            {
                changes.Add(new RawChange("member-added", symbol, memberName, null, member.Signature, "non-breaking", framework));
            }

            foreach (var oldMember in oldMembers)
            {
                var matching = newMembers.FirstOrDefault(member => member.Key == oldMember.Key);
                if (matching is not null && oldMember.Obsolete != matching.Obsolete)
                {
                    changes.Add(new RawChange(matching.Obsolete ? "deprecation-added" : "deprecation-removed", symbol, memberName,
                        oldMember.Obsolete.ToString(), matching.Obsolete.ToString(), "unknown", framework));
                }
                if (matching is not null && oldMember.Accessibility != matching.Accessibility)
                {
                    changes.Add(new RawChange("member-accessibility-changed", symbol, memberName,
                        oldMember.Accessibility, matching.Accessibility, "potentially-breaking", framework));
                }
            }
        }
    }

    private static void AddIfChanged(List<RawChange> changes, string kind, string symbol, string? member, string? before, string? after, string compatibility, string framework)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new RawChange(kind, symbol, member, before, after, compatibility, framework));
        }
    }

    private static string Join(IEnumerable<string> values) => string.Join("; ", values);

    private static string WithoutNullability(string signature)
    {
        var index = signature.IndexOf(" [nullability:", StringComparison.Ordinal);
        return index < 0 ? signature : signature[..index];
    }

    private sealed record RawChange(string Kind, string Symbol, string? Member, string? Before, string? After, string Compatibility, string Framework);
}

public static partial class AssemblyUsageCollector
{
    public static CollectedUsage Collect(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var provider = new MetadataTypeNameProvider(reader);
        var publicApi = AssemblyMetadataReader.ReadPublicApi(reader);
        var exposedTypeNames = BuildExposedTypeNames(publicApi);
        var entries = new Dictionary<string, MutableUsage>(StringComparer.Ordinal);

        foreach (var handle in reader.TypeReferences)
        {
            if (!IsTeamsType(reader, handle)) continue;
            var name = provider.GetTypeFromReference(reader, handle, 0);
            entries.TryAdd(name, new MutableUsage());
        }

        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            var typeHandle = UnwrapTypeReference(reader, member.Parent);
            if (typeHandle.IsNil || !IsTeamsType(reader, typeHandle)) continue;
            var name = provider.GetTypeFromReference(reader, typeHandle, 0);
            if (!entries.TryGetValue(name, out var usage))
            {
                usage = new MutableUsage();
                entries[name] = usage;
            }
            usage.Members.Add(NormalizeMemberName(reader.GetString(member.Name)));
        }

        var usages = entries.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new CollectedUsageEntry(
                item.Key,
                item.Value.Members.Order(StringComparer.Ordinal).ToArray(),
                exposedTypeNames.Contains(item.Key) ? "publicly-exposed" : "internal-only"))
            .ToArray();
        return new CollectedUsage(1, PackageConstants.PackageId, Path.GetFileName(assemblyPath), usages, ReadSourceFiles(assemblyPath));
    }

    internal static IReadOnlySet<string> BuildExposedTypeNames(IEnumerable<ApiSymbolModel> publicApi)
        => publicApi
            .SelectMany(symbol => new[] { symbol.Name, symbol.BaseType ?? string.Empty }
                .Concat(symbol.Interfaces)
                .Concat(symbol.Members.Select(member => member.Signature)))
            .SelectMany(value => QualifiedTypeNameRegex().Matches(value).Select(match => match.Value))
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"(?<![A-Za-z0-9_+.])(?:[A-Za-z_][A-Za-z0-9_]*`?\d*\.)+[A-Za-z_][A-Za-z0-9_]*`?\d*(?:\+[A-Za-z_][A-Za-z0-9_]*`?\d*)*")]
    private static partial Regex QualifiedTypeNameRegex();

    private static IReadOnlyList<string> ReadSourceFiles(string assemblyPath)
    {
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (!File.Exists(pdbPath)) return [];
        using var stream = File.OpenRead(pdbPath);
        Span<byte> signature = stackalloc byte[4];
        if (stream.Read(signature) != signature.Length || !signature.SequenceEqual("BSJB"u8)) return [];
        stream.Position = 0;
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();
        return reader.Documents.Select(handle => Paths.Normalize(reader.GetString(reader.GetDocument(handle).Name)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeMemberName(string name)
    {
        if (name.StartsWith("get_", StringComparison.Ordinal) || name.StartsWith("set_", StringComparison.Ordinal) ||
            name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal))
        {
            return name[(name.IndexOf('_') + 1)..];
        }
        return name;
    }

    private static TypeReferenceHandle UnwrapTypeReference(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeReference) return (TypeReferenceHandle)handle;
        if (handle.Kind != HandleKind.TypeSpecification) return default;
        var blob = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
        while (blob.RemainingBytes > 0)
        {
            var code = blob.ReadSignatureTypeCode();
            if (code is SignatureTypeCode.GenericTypeInstance)
            {
                _ = blob.ReadSignatureTypeCode();
                var entity = blob.ReadTypeHandle();
                return entity.Kind == HandleKind.TypeReference ? (TypeReferenceHandle)entity : default;
            }
            if (code is not (SignatureTypeCode.SZArray or SignatureTypeCode.Array or SignatureTypeCode.Pointer or SignatureTypeCode.ByReference)) break;
        }
        return default;
    }

    private static bool IsTeamsType(MetadataReader reader, TypeReferenceHandle handle)
    {
        var reference = reader.GetTypeReference(handle);
        var scope = reference.ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
        {
            scope = reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        }
        return scope.Kind == HandleKind.AssemblyReference &&
            reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) == PackageConstants.AssemblyName;
    }

    private sealed class MutableUsage
    {
        public HashSet<string> Members { get; } = new(StringComparer.Ordinal);
    }
}

public static class UsageValidator
{
    public static UsageValidation Validate(UsageManifest manifest, CollectedUsage collected, ApiModel model, string repositoryRoot)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var missingSymbols = new List<string>();
        var missingMembers = new List<string>();
        if (manifest.SchemaVersion != 1) errors.Add("Usage manifest must use schemaVersion 1.");
        if (manifest.Package != PackageConstants.PackageId) errors.Add($"Usage manifest must describe {PackageConstants.PackageId}.");
        if (manifest.DeclaredVersion != model.Version) errors.Add($"Manifest version {manifest.DeclaredVersion} does not match API model version {model.Version}.");

        var duplicates = manifest.Usages
            .GroupBy(usage => usage.UpstreamSymbol, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal);
        errors.AddRange(duplicates.Select(symbol => $"Usage manifest declares {symbol} more than once."));
        var manifestBySymbol = manifest.Usages
            .GroupBy(usage => usage.UpstreamSymbol, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var collectedSymbols = collected.Usages.Select(usage => usage.UpstreamSymbol).ToHashSet(StringComparer.Ordinal);
        var collectedFiles = (collected.SourceFiles ?? []).Select(Paths.Normalize).ToArray();
        foreach (var usage in collected.Usages)
        {
            if (!manifestBySymbol.TryGetValue(usage.UpstreamSymbol, out var declared))
            {
                missingSymbols.Add(usage.UpstreamSymbol);
                continue;
            }
            foreach (var member in usage.Members.Where(member => declared.Members.Count > 0 && !declared.Members.Contains(member, StringComparer.Ordinal)))
            {
                missingMembers.Add($"{usage.UpstreamSymbol}.{member}");
            }
            if (usage.Exposure == "publicly-exposed" && declared.Exposure != "publicly-exposed")
            {
                errors.Add($"{usage.UpstreamSymbol} is publicly exposed but the manifest records {declared.Exposure}.");
            }
        }

        var availableSymbols = model.Frameworks.SelectMany(framework => framework.Symbols).Select(symbol => symbol.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var usage in manifest.Usages)
        {
            if (!availableSymbols.Contains(usage.UpstreamSymbol)) errors.Add($"Manifest symbol does not exist in the declared package: {usage.UpstreamSymbol}.");
            if (!collectedSymbols.Contains(usage.UpstreamSymbol)) errors.Add($"Manifest usage is stale; the built extension does not reference: {usage.UpstreamSymbol}.");
            foreach (var file in usage.Files)
            {
                var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, file));
                var sourceRoot = Path.Combine(repositoryRoot, manifest.SourceRoot);
                if (!Paths.IsContainedBy(sourceRoot, fullPath) || !File.Exists(fullPath)) errors.Add($"Invalid manifest source path: {file}.");
                else if (collectedFiles.Length > 0 && !collectedFiles.Any(document => document.EndsWith(Paths.Normalize(file), StringComparison.OrdinalIgnoreCase)))
                    errors.Add($"Manifest source path is not present in the portable PDB: {file}.");
            }
        }

        errors.AddRange(missingSymbols.Select(symbol => $"Collected usage is missing from the manifest: {symbol}."));
        errors.AddRange(missingMembers.Select(member => $"Collected member usage is missing from the manifest: {member}."));
        return new UsageValidation(1, errors.Count == 0, errors, warnings, missingSymbols, missingMembers);
    }
}
