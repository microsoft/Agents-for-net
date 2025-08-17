using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// Provides a collection of utility methods for working with JSON data in the context of A2A.
/// </summary>
internal static partial class A2AJsonUtilities
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> singleton used as the default in JSON serialization operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For Native AOT or applications disabling <see cref="JsonSerializer.IsReflectionEnabledByDefault"/>, this instance
    /// includes source generated contracts for all common exchange types contained in the A2A library.
    /// </para>
    /// <para>
    /// It additionally turns on the following settings:
    /// <list type="number">
    /// <item>Enables <see cref="JsonSerializerDefaults.Web"/> defaults.</item>
    /// <item>Enables <see cref="JsonIgnoreCondition.WhenWritingNull"/> as the default ignore condition for properties.</item>
    /// <item>Enables <see cref="JsonNumberHandling.AllowReadingFromString"/> as the default number handling for number types.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static JsonSerializerOptions DefaultOptions => JsonContext.Default.Options;

    public static JsonSerializerOptions DefaultReflectionOptions = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(JsonContext.Default, new DefaultJsonTypeInfoResolver())
    };

    // Keep in sync with CreateDefaultOptions above.
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString)]

    // JSON-RPC
    [JsonSerializable(typeof(JsonRpcError))]
    [JsonSerializable(typeof(JsonRpcId))]
    [JsonSerializable(typeof(JsonRpcRequest))]
    [JsonSerializable(typeof(JsonRpcResponse))]
    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]

    // A2A
    [JsonSerializable(typeof(AgentCard))]
    [JsonSerializable(typeof(AgentTask))]
    [JsonSerializable(typeof(GetTaskPushNotificationConfigParams))]
    [JsonSerializable(typeof(MessageSendParams))]
    [JsonSerializable(typeof(MessageSendConfiguration))]
    [JsonSerializable(typeof(TaskIdParams))]
    [JsonSerializable(typeof(TaskPushNotificationConfig))]
    [JsonSerializable(typeof(List<TaskPushNotificationConfig>))]
    [JsonSerializable(typeof(TaskQueryParams))]
    [JsonSerializable(typeof(TaskIdParams))]
    [JsonSerializable(typeof(TaskArtifactUpdateEvent))]
    [JsonSerializable(typeof(TaskStatusUpdateEvent))]
    [JsonSerializable(typeof(SendStreamingMessageResponse))]
    [JsonSerializable(typeof(ListTaskPushNotificationConfigParams))]
    [JsonSerializable(typeof(DeleteTaskPushNotificationConfigParams))]
    [JsonSerializable(typeof(APIKeySecurityScheme))]
    [JsonSerializable(typeof(OAuth2SecurityScheme))]
    [JsonSerializable(typeof(OpenIdConnectSecurityScheme))]
    [JsonSerializable(typeof(HTTPAuthSecurityScheme))]
    [JsonSerializable(typeof(PushNotificationAuthenticationInfo))]

    [ExcludeFromCodeCoverage]
    internal sealed partial class JsonContext : JsonSerializerContext;

    public static string ToJson(object obj) => JsonSerializer.Serialize(obj, A2AJsonUtilities.DefaultOptions.GetTypeInfo(obj.GetType()));
}
