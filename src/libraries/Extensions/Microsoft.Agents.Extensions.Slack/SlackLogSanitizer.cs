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
        => ExecuteSafely(() => SanitizeJson(ProtocolJsonSerializer.ToJson(value)), Unavailable);

    internal static void ExecuteSafely(Action loggingAction)
    {
        ExecuteSafely(
            () =>
            {
                loggingAction();
                return true;
            },
            false);
    }

    private static T ExecuteSafely<T>(Func<T> loggingAction, T fallback)
    {
        try
        {
            return loggingAction();
        }
        catch (Exception ex) when (IsSafeToSuppressForLogging(ex))
        {
            return fallback;
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
                    if (IsCredentialBearingUrl(child))
                    {
                        obj[propertyName] = Redacted;
                    }
                    else
                    {
                        Redact(child);
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                var child = array[index];
                if (child != null)
                {
                    if (IsCredentialBearingUrl(child))
                    {
                        array[index] = Redacted;
                    }
                    else
                    {
                        Redact(child);
                    }
                }
            }
        }
    }

    private static bool IsCredentialBearingUrl(JsonNode node)
    {
        if (node is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return true;
        }

        if (uri.Host.Equals("hooks.slack.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/services/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = parameter.IndexOf('=');
            var encodedName = separatorIndex >= 0 ? parameter[..separatorIndex] : parameter;
            var name = Uri.UnescapeDataString(encodedName.Replace("+", " ", StringComparison.Ordinal));
            var normalizedName = new string(name.Where(char.IsLetterOrDigit).ToArray());

            if (IsSensitiveQueryParameter(normalizedName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSensitiveQueryParameter(string normalizedName)
    {
        return normalizedName.EndsWith("sig", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("token", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("secret", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("signature", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("key", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("authorization", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitiveProperty(string normalizedPropertyName)
    {
        return normalizedPropertyName.Equals("authorization", StringComparison.OrdinalIgnoreCase)
            || normalizedPropertyName.Equals("responseurl", StringComparison.OrdinalIgnoreCase)
            || normalizedPropertyName.Equals("uploadurl", StringComparison.OrdinalIgnoreCase)
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
