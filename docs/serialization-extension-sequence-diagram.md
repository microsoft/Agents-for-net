# Serialization Extension Sequence Diagram

Shows how the SDK discovers and registers serialization extensions (custom converters, type resolvers, and Entity subclasses) by using assembly-level attributes generated at build time by Roslyn source generators and scanned at runtime.

## Participants

- **Developer** — Authors a library or extension that adds custom serialization (converters, entities).
- **Source Generators** — Compile-time Roslyn source generators that emit assembly attributes.
- **Assembly Attributes** — Generated `[assembly: ...]` markers that the runtime scans.
- **ProtocolJsonSerializer** — The central static serializer that owns `SerializationOptions` and `EntityTypes`.

## Two Extension Paths

| Path | Purpose | Developer Action | Generated Attribute | Runtime Effect |
|------|---------|-----------------|---------------------|----------------|
| **Serialization Init** | Register converters / resolvers | Decorate a class or struct with `[SerializationInit]`, add a `public static void Init()` method | `SerializationInitAssemblyAttribute` | `Init()` is called → type calls `ApplyExtensionConverters` or `AddTypeInfoResolver` |
| **Entity Init** | Register Entity subclasses for polymorphic deserialization | Subclass `Entity`; optionally add `[EntityName("name")]` | `EntityInitAssemblyAttribute` | Type is registered in `EntityTypes` dictionary |

## Serialization Init Flow

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant SG as SerializationInitSourceGenerator
    participant Asm as Assembly (compiled)
    participant SIA as SerializationInitAssemblyAttribute
    participant PJS as ProtocolJsonSerializer
    participant InitType as [SerializationInit] Type

    Note over Dev,SG: Compile Time
    Dev->>SG: Decorates type with [SerializationInit]
    SG->>SG: Finds all classes/structs with SerializationInitAttribute
    SG->>Asm: Emits [assembly: SerializationInitAssemblyAttribute(typeof(InitType))]

    Note over Asm,PJS: Runtime — Static Constructor
    PJS->>PJS: Static ctor fires on first access
    PJS->>SIA: SerializationInitAssemblyAttribute.InitSerialization()
    SIA->>SIA: Scans loaded assemblies for SerializationInitAssemblyAttribute
    SIA->>SIA: Hooks AppDomain.AssemblyLoad for late-loaded assemblies
    loop For each assembly with attribute
        SIA->>InitType: Invokes static Init() via reflection
        InitType->>PJS: ApplyExtensionConverters(converters) or AddTypeInfoResolver(resolver)
        PJS->>PJS: Replaces SerializationOptions (copy-on-write under lock)
    end
```

## Entity Init Flow

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant EG as EntityInitSourceGenerator
    participant Asm as Assembly (compiled)
    participant EIA as EntityInitAssemblyAttribute
    participant PJS as ProtocolJsonSerializer

    Note over Dev,EG: Compile Time
    Dev->>EG: Creates class that inherits from Entity
    Dev->>EG: Optionally adds [EntityName("customName")]
    EG->>EG: Finds all classes inheriting from Entity
    EG->>Asm: Emits [assembly: EntityInitAssemblyAttribute(typeof(MyEntity))]

    Note over Asm,PJS: Runtime — Static Constructor
    PJS->>PJS: Static ctor fires on first access
    PJS->>EIA: EntityInitAssemblyAttribute.InitSerialization()
    EIA->>EIA: Scans loaded assemblies for EntityInitAssemblyAttribute
    EIA->>EIA: Hooks AppDomain.AssemblyLoad for late-loaded assemblies
    loop For each Entity subclass
        alt Has [EntityName("x")]
            EIA->>PJS: EntityTypes["x"] = typeof(MyEntity)
        else No EntityName attribute
            EIA->>PJS: EntityTypes["MyEntity"] = typeof(MyEntity)
        end
    end
```

## Combined Initialization Sequence

```mermaid
sequenceDiagram
    participant App as Application Start
    participant PJS as ProtocolJsonSerializer
    participant SIA as SerializationInitAssemblyAttribute
    participant EIA as EntityInitAssemblyAttribute
    participant AD as AppDomain

    App->>PJS: First access triggers static ctor
    PJS->>PJS: InitSerializerOptions() — sets core converters + CoreJsonContext
    PJS->>PJS: CoreEntities() — registers built-in entity types
    PJS->>SIA: InitSerialization()
    SIA->>AD: Hook AssemblyLoad event
    SIA->>AD: GetAssemblies() — scan all loaded assemblies
    loop Each assembly with SerializationInitAssemblyAttribute
        SIA->>SIA: Reflect Init() method, invoke it
    end
    PJS->>EIA: InitSerialization()
    EIA->>AD: Hook AssemblyLoad event
    EIA->>AD: GetAssemblies() — scan all loaded assemblies
    loop Each assembly with EntityInitAssemblyAttribute
        EIA->>EIA: Check for [EntityName], register in EntityTypes
    end
    Note over PJS: Ready — SerializationOptions and EntityTypes fully populated
```

## Developer Usage Examples

### Adding Custom Converters (e.g., Builder.Dialogs)

```csharp
[SerializationInit]
internal class SerializationInit
{
    public static void Init()
    {
        var converters = new List<JsonConverter>
        {
            new PersistedStateConverter(),
            new Array2DConverter()
        };
        ProtocolJsonSerializer.ApplyExtensionConverters(converters);
    }
}
```

### Adding a Custom Entity

```csharp
[EntityName("clientInfo")]
public class ClientInfo : Entity
{
    public const string EntityName = "clientInfo";
    public ClientInfo() : base(EntityName) { }

    public string? Locale { get; set; }
    public string? Country { get; set; }
}
```

No explicit registration code is needed — the source generator detects the `Entity` subclass at compile time and the runtime registers it automatically.

### Adding a Source-Generated JsonSerializerContext

```csharp
[SerializationInit]
internal class SerializationInit
{
    public static void Init()
    {
        ProtocolJsonSerializer.AddTypeInfoResolver(MyExtensionJsonContext.Default);
    }
}
```

## Key Design Points

- **Compile-time discovery** — Source generators run during build, eliminating reflection-heavy type scanning at startup.
- **Late-loading support** — `AppDomain.AssemblyLoad` hook ensures assemblies loaded after initial startup (common with lazy-loaded NuGet packages) are also initialized.
- **Copy-on-write thread safety** — `ProtocolJsonSerializer` replaces `SerializationOptions` atomically under a lock; concurrent readers never see a partially-mutated instance.
- **Entity polymorphism** — `EntityConverter` uses the `EntityTypes` dictionary to deserialize `Entity` objects to their concrete types based on the `type` field in JSON.
- **No manual registration** — Developers only need to inherit from `Entity` or add `[SerializationInit]`; the generators and runtime handle wiring.
