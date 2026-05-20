// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Base class for Slack model objects that expose dot-notation path navigation via
/// <see cref="Get{T}"/> and <see cref="TryGet{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Subclasses that are plain POCOs (e.g. <see cref="SlackResponse"/>, <see cref="EventEnvelope"/>)
/// rely on the default <see cref="GetData"/> implementation, which lazily serializes the instance
/// to a <see cref="JsonObject"/> on first use so every field — including <c>[JsonExtensionData]</c>
/// catch-alls — is reachable by path. Subclasses that are themselves backed by a
/// <see cref="JsonObject"/> (e.g. <see cref="EventContent"/>) override <see cref="GetData"/> to
/// return that object directly, avoiding redundant serialization.
/// </para>
/// <para>
/// Subclasses whose JSON field names differ from their C# property names (e.g.
/// <see cref="EventEnvelope"/> maps <c>"event_content"</c> → <c>"event"</c>) override
/// <see cref="NormalizePath"/> to remap the alias before navigation.
/// </para>
/// </remarks>
public abstract class SlackModel
{
    // Lazily-cached serialized form. Only used by the default GetData() implementation
    // (POCO-backed subclasses). JsonObject-backed subclasses bypass this entirely by
    // overriding GetData(). Not thread-safe for concurrent first-initialization, but
    // harmless if two threads each initialize once — both results are equivalent.
    private JsonObject _lazyData;

    /// <summary>
    /// Returns the <see cref="JsonObject"/> used for path navigation.
    /// The default implementation lazily serializes this instance via
    /// <c>JsonSerializer.SerializeToNode</c>, making every property and
    /// <c>[JsonExtensionData]</c> field addressable. Override when the subclass is already
    /// backed by a <see cref="JsonObject"/> to return it directly.
    /// </summary>
    protected virtual JsonObject GetData()
        => _lazyData ??= (JsonObject)JsonSerializer.SerializeToNode(this, GetType());

    /// <summary>
    /// Normalizes a caller-supplied path before it is passed to the navigator.
    /// Override to remap C# property-name aliases to their JSON field names.
    /// The default implementation returns the path unchanged.
    /// </summary>
    protected virtual string NormalizePath(string path) => path;

    /// <summary>
    /// Gets a value at the given dot-notation path. Supports dot-separated property
    /// access and bracket array indexing (e.g. <c>"message.attachments[0].text"</c>).
    /// Returns <see langword="default"/> when the path does not exist or the value
    /// cannot be converted to <typeparamref name="T"/>.
    /// </summary>
    public T Get<T>(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            try { return GetData().Deserialize<T>(); } catch { return default; }
        }

        ObjectPath.TryGetPathValue<T>(GetData(), NormalizePath(path), out var value);
        return value;
    }

    /// <summary>
    /// Tries to get a value at the given dot-notation path.
    /// Returns <see langword="false"/> when the path does not exist or the value
    /// cannot be converted to <typeparamref name="T"/>.
    /// </summary>
    public bool TryGet<T>(string path, out T value)
    {
        if (string.IsNullOrEmpty(path))
        {
            try { value = GetData().Deserialize<T>(); return true; }
            catch { value = default; return false; }
        }

        return ObjectPath.TryGetPathValue<T>(GetData(), NormalizePath(path), out value);
    }
}
