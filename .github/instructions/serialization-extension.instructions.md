---
applyTo:
  - "**/SerializationInit*"
  - "**/EntityInit*"
  - "**/EntityName*"
  - "**/ProtocolJsonSerializer*"
  - "**/Serialization/Converters/**"
  - "**/CoreJsonContext*"
---

# Serialization Extension Context

When working on serialization infrastructure, read `docs/serialization-extension-sequence-diagram.md` for the full mermaid sequence diagrams covering compile-time generation, runtime initialization, and extension patterns.

## Key Design Points

- **`ProtocolJsonSerializer`** is a static class with `SerializationOptions` (System.Text.Json) and `EntityTypes` dictionary. All SDK serialization routes through it.
- **`[SerializationInit]`** marks a class/struct whose `public static void Init()` method will be called when `ProtocolJsonSerializer` is first accessed (via its static constructor). Use it to register converters via `ApplyExtensionConverters` or resolvers via `AddTypeInfoResolver`.
- **`[EntityName("name")]`** overrides the key used to register an Entity subclass in `EntityTypes`. Without it, the class name is used.
- **Source generators** (`SerializationInitSourceGenerator`, `EntityInitSourceGenerator`) emit assembly-level attributes at compile time — no explicit `[assembly: ...]` lines are authored by developers.
- **Thread safety** — All mutations to `SerializationOptions` use copy-on-write under `_optionsLock`. Never mutate the current `SerializationOptions` instance directly; always go through `ApplyExtensionConverters`, `AddTypeInfoResolver`, or `ApplyExtensionOptions`.
- **Late-loaded assemblies** — Both init attributes hook `AppDomain.AssemblyLoad` to handle assemblies loaded after the static constructor runs.

## Conventions

- Extension libraries place their `[SerializationInit]` class in a `Serialization/` subfolder (e.g., `Microsoft.Agents.Extensions.MSTeams/Serialization/SerializationInit.cs`).
- The `Init()` method must be `public static void Init()` — the runtime invokes it by reflection.
- Custom `JsonConverter` classes live under `Serialization/Converters/`.
- Entity subclasses need only inherit from `Entity`; the `EntityInitSourceGenerator` handles registration automatically.
- When replacing `TypeInfoResolver`, always include `CoreJsonContext.Default` in the chain via `JsonTypeInfoResolver.Combine(...)`.
- Use `System.Text.Json` exclusively — Newtonsoft.Json is not used in this SDK.
