using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed class PackageApiService
{
    private static readonly string[] TargetFrameworks = ["net8.0", "net10.0"];
    private const string DefaultSource = "https://api.nuget.org/v3/index.json";
    private readonly IReadOnlyList<SourceRepository> _repositories;

    public PackageApiService(IEnumerable<string>? sources = null, string? configFile = null)
    {
        _repositories = ResolveSources(sources, configFile)
            .Select(source => Repository.CreateSource(Repository.Provider.GetCoreV3(), source))
            .ToArray();
    }

    public async Task<string> GetLatestStableVersionAsync(CancellationToken cancellationToken = default)
    {
        using var cache = new SourceCacheContext();
        var versions = new List<NuGetVersion>();
        foreach (var repository in _repositories)
        {
            var resource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"NuGet metadata resource is unavailable for {repository.PackageSource.Source}.");
            var metadata = await resource.GetMetadataAsync(
                PackageConstants.PackageId,
                includePrerelease: false,
                includeUnlisted: false,
                cache,
                NullLogger.Instance,
                cancellationToken).ConfigureAwait(false);
            versions.AddRange(metadata.Select(item => item.Identity.Version));
        }

        var latest = versions
            .Where(version => !version.IsPrerelease)
            .OrderBy(version => version)
            .LastOrDefault();
        return latest?.ToNormalizedString()
            ?? throw new InvalidOperationException($"No listed stable release exists for {PackageConstants.PackageId}.");
    }

    public async Task<ApiModel> ExtractAsync(string version, CancellationToken cancellationToken = default)
    {
        if (!NuGetVersion.TryParse(version, out var parsedVersion))
        {
            throw new ArgumentException($"Invalid NuGet version: {version}", nameof(version));
        }

        using var cache = new SourceCacheContext();
        var sourceFailures = new List<string>();
        foreach (var repository in _repositories)
        {
            try
            {
                var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"NuGet package resource is unavailable for {repository.PackageSource.Source}.");
                await using var packageStream = new MemoryStream();
                var copied = await resource.CopyNupkgToStreamAsync(
                    PackageConstants.PackageId,
                    parsedVersion,
                    packageStream,
                    cache,
                    NullLogger.Instance,
                    cancellationToken).ConfigureAwait(false);
                if (!copied)
                {
                    continue;
                }

                packageStream.Position = 0;
                using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
                var frameworks = TargetFrameworks.Select(tfm => ExtractFramework(archive, tfm)).ToArray();
                return new ApiModel(1, PackageConstants.PackageId, parsedVersion.ToNormalizedString(), frameworks);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                sourceFailures.Add($"{repository.PackageSource.Source}: {exception.Message}");
            }
        }

        var failures = sourceFailures.Count == 0 ? string.Empty : $" Source failures: {string.Join(" | ", sourceFailures)}.";
        throw new InvalidOperationException(
            $"Could not find {PackageConstants.PackageId}@{version} in: {string.Join(", ", _repositories.Select(item => item.PackageSource.Source))}.{failures}");
    }

    internal static IReadOnlyList<PackageSource> ResolveSources(IEnumerable<string>? sources, string? configFile)
    {
        var requested = sources?.ToArray() ?? [];
        if (requested.Length == 0)
        {
            requested = [DefaultSource];
        }

        IReadOnlyList<PackageSource> configured = [];
        if (configFile is not null)
        {
            var fullPath = Path.GetFullPath(configFile);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("NuGet configuration file was not found.", fullPath);
            }

            var settings = Settings.LoadSpecificSettings(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath));
            configured = new PackageSourceProvider(settings).LoadPackageSources().ToArray();
        }

        return requested.Select(value =>
        {
            var match = configured.FirstOrDefault(source =>
                string.Equals(source.Name, value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source.Source, value, StringComparison.OrdinalIgnoreCase));
            return match ?? new PackageSource(value);
        }).ToArray();
    }

    private static FrameworkApiModel ExtractFramework(ZipArchive archive, string targetFramework)
    {
        var selected = SelectAsset(archive.Entries, targetFramework);
        if (selected is null)
        {
            return new FrameworkApiModel(targetFramework, null, []);
        }

        using var stream = selected.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        using var peReader = new PEReader(copy);
        if (!peReader.HasMetadata)
        {
            throw new InvalidDataException($"{selected.FullName} is not a managed assembly.");
        }

        return new FrameworkApiModel(
            targetFramework,
            selected.FullName,
            AssemblyMetadataReader.ReadPublicApi(peReader.GetMetadataReader()));
    }

    internal static ZipArchiveEntry? SelectAsset(IEnumerable<ZipArchiveEntry> entries, string targetFramework)
    {
        var candidates = entries
            .Where(entry => entry.FullName.EndsWith($"/{PackageConstants.AssemblyName}.dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => new { Entry = entry, Parts = entry.FullName.Split('/') })
            .Where(item => item.Parts.Length == 3 && (item.Parts[0] == "ref" || item.Parts[0] == "lib"))
            .ToArray();
        var reducer = new FrameworkReducer();
        var target = NuGetFramework.ParseFolder(targetFramework);
        foreach (var root in new[] { "ref", "lib" })
        {
            var group = candidates.Where(item => item.Parts[0] == root)
                .Select(item => (Item: item, Framework: NuGetFramework.ParseFolder(item.Parts[1])))
                .Where(item => !item.Framework.IsUnsupported)
                .ToArray();
            var nearest = reducer.GetNearest(target, group.Select(item => item.Framework));
            var match = group.FirstOrDefault(item => item.Framework.Equals(nearest));
            if (match.Item is not null)
            {
                return match.Item.Entry;
            }
        }

        return null;
    }
}

internal static class AssemblyMetadataReader
{
    public static IReadOnlyList<ApiSymbolModel> ReadPublicApi(MetadataReader reader)
    {
        var provider = new MetadataTypeNameProvider(reader);
        return reader.TypeDefinitions
            .Select(handle => ReadType(reader, provider, handle))
            .Where(model => model is not null)
            .Cast<ApiSymbolModel>()
            .OrderBy(model => model.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static ApiSymbolModel? ReadType(MetadataReader reader, MetadataTypeNameProvider provider, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        var accessibility = TypeAccessibility(definition.Attributes);
        if (accessibility is null || reader.GetString(definition.Name) == "<Module>")
        {
            return null;
        }

        var name = provider.GetTypeFromDefinition(reader, handle, 0);
        var kind = Kind(reader, definition);
        var nullableContext = Nullability(reader, definition.GetCustomAttributes());
        var interfaces = definition.GetInterfaceImplementations()
            .Select(item => provider.Format(reader.GetInterfaceImplementation(item).Interface))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var constraints = ReadGenericConstraints(reader, provider, definition.GetGenericParameters());
        var members = new List<ApiMemberModel>();

        foreach (var fieldHandle in definition.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            var fieldAccessibility = FieldAccessibility(field.Attributes);
            if (fieldAccessibility is null || (field.Attributes & FieldAttributes.SpecialName) != 0)
            {
                continue;
            }

            var fieldName = reader.GetString(field.Name);
            var signature = WithNullability(field.DecodeSignature(provider, null), nullableContext, Nullability(reader, field.GetCustomAttributes()));
            var literal = field.GetDefaultValue().IsNil ? null : ReadConstant(reader, field.GetDefaultValue());
            if (literal is not null)
            {
                signature += $" = {literal}";
            }

            members.Add(Member("field", fieldName, fieldAccessibility, signature, HasObsolete(reader, field.GetCustomAttributes())));
        }

        foreach (var methodHandle in definition.GetMethods())
        {
            var method = reader.GetMethodDefinition(methodHandle);
            var methodAccessibility = MethodAccessibility(method.Attributes);
            var methodName = reader.GetString(method.Name);
            if (methodAccessibility is null ||
                ((method.Attributes & MethodAttributes.SpecialName) != 0 &&
                 methodName is not ".ctor" and not ".cctor" &&
                 !methodName.StartsWith("op_", StringComparison.Ordinal)))
            {
                continue;
            }

            var decoded = method.DecodeSignature(provider, null);
            var parameters = ReadParameters(reader, method, decoded.ParameterTypes);
            var generic = decoded.GenericParameterCount > 0 ? $"<{decoded.GenericParameterCount}>" : string.Empty;
            var parameterNullability = method.GetParameters()
                .Select(parameter => Nullability(reader, reader.GetParameter(parameter).GetCustomAttributes()));
            var signature = WithNullability($"{decoded.ReturnType} {methodName}{generic}({string.Join(", ", parameters)})",
                nullableContext, Nullability(reader, method.GetCustomAttributes()), string.Join(";", parameterNullability));
            var methodConstraints = ReadGenericConstraints(reader, provider, method.GetGenericParameters());
            if (methodConstraints.Count > 0)
            {
                signature += $" where {string.Join("; ", methodConstraints)}";
            }

            members.Add(Member("method", methodName, methodAccessibility, signature, HasObsolete(reader, method.GetCustomAttributes())));
        }

        foreach (var propertyHandle in definition.GetProperties())
        {
            var property = reader.GetPropertyDefinition(propertyHandle);
            var accessors = property.GetAccessors();
            var accessibilityValue = AccessorAccessibility(reader, accessors.Getter, accessors.Setter);
            if (accessibilityValue is null)
            {
                continue;
            }

            var decoded = property.DecodeSignature(provider, null);
            var propertyName = reader.GetString(property.Name);
            var accessorText = $"{{ {(accessors.Getter.IsNil ? string.Empty : "get; ")}{(accessors.Setter.IsNil ? string.Empty : "set; ")} }}";
            var signature = decoded.ParameterTypes.Length == 0
                ? $"{decoded.ReturnType} {propertyName} {accessorText}"
                : $"{decoded.ReturnType} {propertyName}[{string.Join(", ", decoded.ParameterTypes)}] {accessorText}";
            signature = WithNullability(signature, nullableContext, Nullability(reader, property.GetCustomAttributes()));
            members.Add(Member("property", propertyName, accessibilityValue, signature, HasObsolete(reader, property.GetCustomAttributes())));
        }

        foreach (var eventHandle in definition.GetEvents())
        {
            var eventDefinition = reader.GetEventDefinition(eventHandle);
            var accessors = eventDefinition.GetAccessors();
            var accessibilityValue = AccessorAccessibility(reader, accessors.Adder, accessors.Remover);
            if (accessibilityValue is null)
            {
                continue;
            }

            var eventName = reader.GetString(eventDefinition.Name);
            var signature = WithNullability($"{provider.Format(eventDefinition.Type)} {eventName}", nullableContext,
                Nullability(reader, eventDefinition.GetCustomAttributes()));
            members.Add(Member("event", eventName, accessibilityValue, signature, HasObsolete(reader, eventDefinition.GetCustomAttributes())));
        }

        return new ApiSymbolModel(
            name,
            kind,
            accessibility,
            definition.BaseType.IsNil ? null : provider.Format(definition.BaseType),
            interfaces,
            constraints,
            HasObsolete(reader, definition.GetCustomAttributes()),
            members.OrderBy(member => member.Key, StringComparer.Ordinal).ToArray());
    }

    private static ApiMemberModel Member(string kind, string name, string accessibility, string signature, bool obsolete)
        => new($"{kind}:{name}:{signature}", name, kind, accessibility, signature, obsolete);

    private static IReadOnlyList<string> ReadParameters(MetadataReader reader, MethodDefinition method, ImmutableArray<string> parameterTypes)
    {
        var parameters = method.GetParameters()
            .Select(handle => reader.GetParameter(handle))
            .Where(parameter => parameter.SequenceNumber > 0)
            .OrderBy(parameter => parameter.SequenceNumber)
            .ToArray();
        return parameterTypes.Select((type, index) =>
        {
            var parameter = index < parameters.Length ? parameters[index] : default;
            var attributes = index < parameters.Length ? parameter.Attributes : 0;
            var modifier = (attributes & ParameterAttributes.Out) != 0 ? "out " : (attributes & ParameterAttributes.In) != 0 ? "in " : string.Empty;
            var optional = (attributes & ParameterAttributes.Optional) != 0 ? " optional" : string.Empty;
            var name = index < parameters.Length ? reader.GetString(parameter.Name) : $"arg{index}";
            var defaultValue = index < parameters.Length && !parameter.GetDefaultValue().IsNil
                ? $" = {ReadConstant(reader, parameter.GetDefaultValue())}"
                : string.Empty;
            return $"{modifier}{type} {name}{optional}{defaultValue}";
        }).ToArray();
    }

    private static IReadOnlyList<string> ReadGenericConstraints(MetadataReader reader, MetadataTypeNameProvider provider, GenericParameterHandleCollection handles)
    {
        var result = new List<string>();
        foreach (var handle in handles)
        {
            var parameter = reader.GetGenericParameter(handle);
            var constraints = parameter.GetConstraints()
                .Select(item => provider.Format(reader.GetGenericParameterConstraint(item).Type))
                .ToList();
            if ((parameter.Attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Insert(0, "class");
            if ((parameter.Attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Insert(0, "struct");
            if ((parameter.Attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) constraints.Add("new()");
            if (constraints.Count > 0)
            {
                result.Add($"{reader.GetString(parameter.Name)}: {string.Join(", ", constraints)}");
            }
        }

        return result;
    }

    private static string Kind(MetadataReader reader, TypeDefinition definition)
    {
        if ((definition.Attributes & TypeAttributes.Interface) != 0) return "interface";
        var baseType = definition.BaseType.IsNil ? string.Empty : new MetadataTypeNameProvider(reader).Format(definition.BaseType);
        if (baseType == "System.Enum") return "enum";
        if (baseType == "System.MulticastDelegate") return "delegate";
        if (baseType == "System.ValueType") return "struct";
        return "class";
    }

    private static string? TypeAccessibility(TypeAttributes attributes) => (attributes & TypeAttributes.VisibilityMask) switch
    {
        TypeAttributes.Public or TypeAttributes.NestedPublic => "public",
        TypeAttributes.NestedFamily => "protected",
        TypeAttributes.NestedFamORAssem => "protected-internal",
        _ => null
    };

    private static string? MethodAccessibility(MethodAttributes attributes) => (attributes & MethodAttributes.MemberAccessMask) switch
    {
        MethodAttributes.Public => "public",
        MethodAttributes.Family => "protected",
        MethodAttributes.FamORAssem => "protected-internal",
        _ => null
    };

    private static string? FieldAccessibility(FieldAttributes attributes) => (attributes & FieldAttributes.FieldAccessMask) switch
    {
        FieldAttributes.Public => "public",
        FieldAttributes.Family => "protected",
        FieldAttributes.FamORAssem => "protected-internal",
        _ => null
    };

    private static string? AccessorAccessibility(MetadataReader reader, params MethodDefinitionHandle[] handles)
        => handles.Where(handle => !handle.IsNil)
            .Select(handle => MethodAccessibility(reader.GetMethodDefinition(handle).Attributes))
            .FirstOrDefault(value => value is not null);

    private static bool HasObsolete(MetadataReader reader, CustomAttributeHandleCollection attributes)
        => attributes.Any(handle => AttributeTypeName(reader, reader.GetCustomAttribute(handle).Constructor) == "System.ObsoleteAttribute");

    private static string WithNullability(string signature, params string[] annotations)
    {
        var value = string.Join(";", annotations.Where(annotation => annotation.Length > 0));
        return value.Length == 0 ? signature : $"{signature} [nullability:{value}]";
    }

    private static string Nullability(MetadataReader reader, CustomAttributeHandleCollection attributes)
    {
        return string.Join(",", attributes
            .Select(reader.GetCustomAttribute)
            .Where(attribute => AttributeTypeName(reader, attribute.Constructor) is
                "System.Runtime.CompilerServices.NullableAttribute" or "System.Runtime.CompilerServices.NullableContextAttribute")
            .Select(attribute => Convert.ToHexString(reader.GetBlobBytes(attribute.Value)))
            .Order(StringComparer.Ordinal));
    }

    private static string AttributeTypeName(MetadataReader reader, EntityHandle constructor)
    {
        if (constructor.Kind == HandleKind.MemberReference)
        {
            return new MetadataTypeNameProvider(reader).Format(reader.GetMemberReference((MemberReferenceHandle)constructor).Parent);
        }

        if (constructor.Kind == HandleKind.MethodDefinition)
        {
            var method = reader.GetMethodDefinition((MethodDefinitionHandle)constructor);
            return new MetadataTypeNameProvider(reader).GetTypeFromDefinition(reader, method.GetDeclaringType(), 0);
        }

        return string.Empty;
    }

    private static string ReadConstant(MetadataReader reader, ConstantHandle handle)
    {
        var constant = reader.GetConstant(handle);
        if (constant.Value.IsNil) return "null";
        var blob = reader.GetBlobReader(constant.Value);
        object? value = constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => blob.ReadBoolean(),
            ConstantTypeCode.Char => (char)blob.ReadUInt16(),
            ConstantTypeCode.SByte => blob.ReadSByte(),
            ConstantTypeCode.Byte => blob.ReadByte(),
            ConstantTypeCode.Int16 => blob.ReadInt16(),
            ConstantTypeCode.UInt16 => blob.ReadUInt16(),
            ConstantTypeCode.Int32 => blob.ReadInt32(),
            ConstantTypeCode.UInt32 => blob.ReadUInt32(),
            ConstantTypeCode.Int64 => blob.ReadInt64(),
            ConstantTypeCode.UInt64 => blob.ReadUInt64(),
            ConstantTypeCode.Single => blob.ReadSingle(),
            ConstantTypeCode.Double => blob.ReadDouble(),
            ConstantTypeCode.String => blob.ReadUTF16(blob.Length),
            ConstantTypeCode.NullReference => null,
            _ => Convert.ToHexString(blob.ReadBytes(blob.Length))
        };
        return value is string text ? $"\"{text}\"" : value?.ToString() ?? "null";
    }
}

internal sealed class MetadataTypeNameProvider(MetadataReader reader) : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
    public string GetByReferenceType(string elementType) => $"{elementType}&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => $"fnptr({string.Join(",", signature.ParameterTypes)})->{signature.ReturnType}";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(",", typeArguments)}>";
    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => $"{unmodifiedType} mod{(isRequired ? "req" : "opt")}({modifier})";
    public string GetPinnedType(string elementType) => $"{elementType} pinned";
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Void => "System.Void",
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        _ => typeCode.ToString()
    };
    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetTypeFromDefinition(MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var definition = metadataReader.GetTypeDefinition(handle);
        var name = metadataReader.GetString(definition.Name);
        if (!definition.GetDeclaringType().IsNil)
        {
            return $"{GetTypeFromDefinition(metadataReader, definition.GetDeclaringType(), 0)}+{name}";
        }
        var ns = metadataReader.GetString(definition.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }
    public string GetTypeFromReference(MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var reference = metadataReader.GetTypeReference(handle);
        var name = metadataReader.GetString(reference.Name);
        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return $"{GetTypeFromReference(metadataReader, (TypeReferenceHandle)reference.ResolutionScope, 0)}+{name}";
        }
        var ns = metadataReader.GetString(reference.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }
    public string GetTypeFromSpecification(MetadataReader metadataReader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => metadataReader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    public string Format(EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
        _ => handle.Kind.ToString()
    };
}
