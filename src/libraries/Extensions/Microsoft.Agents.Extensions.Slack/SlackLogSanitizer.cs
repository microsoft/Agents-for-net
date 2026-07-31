// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Agents.Core.Serialization;

[assembly: InternalsVisibleTo("Microsoft.Agents.Extensions.Slack.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]

namespace Microsoft.Agents.Extensions.Slack;

internal static class SlackLogSanitizer
{
    private const string Redacted = "[REDACTED]";
    private const string Unavailable = "[UNAVAILABLE]";

    internal static string SanitizeObject(object value)
    {
        try
        {
            return SanitizeJson(ProtocolJsonSerializer.ToJson(value));
        }
        catch (Exception ex) when (IsSafeToSuppressForLogging(ex))
        {
            return Unavailable;
        }
    }

    internal static string SanitizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unavailable;
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return Unavailable;
            }

            Redact(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return Unavailable;
        }
        catch (ArgumentException)
        {
            return Unavailable;
        }
    }

    private static void Redact(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var propertyName in obj.Select(property => property.Key).ToArray())
            {
                var normalizedPropertyName = propertyName
                    .Replace("_", string.Empty, StringComparison.Ordinal)
                    .Replace("-", string.Empty, StringComparison.Ordinal);

                if (IsSensitiveProperty(normalizedPropertyName))
                {
                    obj[propertyName] = Redacted;
                }
                else if (obj[propertyName] is JsonNode child)
                {
                    Redact(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child != null)
                {
                    Redact(child);
                }
            }
        }
    }

    private static bool IsSensitiveProperty(string normalizedPropertyName)
    {
        return normalizedPropertyName.Equals("authorization", StringComparison.OrdinalIgnoreCase)
            || normalizedPropertyName.Equals("responseurl", StringComparison.OrdinalIgnoreCase)
            || normalizedPropertyName.EndsWith("token", StringComparison.OrdinalIgnoreCase)
            || normalizedPropertyName.EndsWith("secret", StringComparison.OrdinalIgnoreCase)
            || normalizedPropertyName.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeToSuppressForLogging(Exception exception)
    {
        return exception is not (
            OperationCanceledException
            or ThreadInterruptedException
#pragma warning disable CS0618
            or ExecutionEngineException
#pragma warning restore CS0618
            or OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or ThreadAbortException
            or AppDomainUnloadedException
            or CannotUnloadAppDomainException
            or BadImageFormatException
            or InvalidProgramException
            or SEHException);
    }
}
