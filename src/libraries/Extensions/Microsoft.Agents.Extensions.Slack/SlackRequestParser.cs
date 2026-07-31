// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Net;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Slack
{
    internal enum SlackRequestKind
    {
        Ignore,
        UrlVerification,
        Event,
        Interactive,
    }

    internal sealed record ParsedSlackRequest(
        SlackRequestKind Kind,
        string? PayloadJson,
        string? Challenge = null,
        EventEnvelope? EventEnvelope = null,
        ActionPayload? ActionPayload = null);

    internal sealed class SlackRequestParser
    {
        public ParsedSlackRequest Parse(string body, string? contentType)
        {
            if (IsFormUrlEncoded(contentType))
            {
                var payloadJson = ExtractFormValue(body, "payload");
                if (string.IsNullOrEmpty(payloadJson))
                {
                    return new ParsedSlackRequest(SlackRequestKind.Ignore, null);
                }

                var actionPayload = ProtocolJsonSerializer.ToObject<ActionPayload>(payloadJson);
                return new ParsedSlackRequest(
                    SlackRequestKind.Interactive,
                    payloadJson,
                    ActionPayload: actionPayload);
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (string.Equals(type, "url_verification", StringComparison.Ordinal))
            {
                var challenge = root.TryGetProperty("challenge", out var challengeElement)
                    ? challengeElement.GetString()
                    : string.Empty;
                return new ParsedSlackRequest(
                    SlackRequestKind.UrlVerification,
                    body,
                    Challenge: challenge);
            }

            if (!string.Equals(type, "event_callback", StringComparison.Ordinal))
            {
                return new ParsedSlackRequest(SlackRequestKind.Ignore, body);
            }

            var eventEnvelope = ProtocolJsonSerializer.ToObject<EventEnvelope>(body);
            return new ParsedSlackRequest(
                SlackRequestKind.Event,
                body,
                EventEnvelope: eventEnvelope);
        }

        private static bool IsFormUrlEncoded(string? contentType)
        {
            return !string.IsNullOrEmpty(contentType)
                && contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtractFormValue(string body, string key)
        {
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            foreach (var pair in body.Split('&'))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var name = pair.Substring(0, separator);
                if (string.Equals(name, key, StringComparison.Ordinal))
                {
                    return WebUtility.UrlDecode(pair.Substring(separator + 1));
                }
            }

            return null;
        }
    }
}
