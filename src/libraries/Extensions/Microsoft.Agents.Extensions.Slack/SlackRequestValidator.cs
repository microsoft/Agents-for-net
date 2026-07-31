// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Agents.Extensions.Slack
{
    internal sealed class SlackRequestValidator
    {
        private readonly SlackAdapterOptions _options;

        internal SlackRequestValidator(SlackAdapterOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        internal bool Verify(HttpRequest request, string body)
        {
            if (string.IsNullOrEmpty(_options.SigningSecret))
            {
                return true;
            }

            var signature = request.Headers["X-Slack-Signature"].ToString();
            var timestamp = request.Headers["X-Slack-Request-Timestamp"].ToString();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || !long.TryParse(timestamp, out var requestUnixTime))
            {
                return false;
            }

            var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - requestUnixTime);
            if (age > _options.RequestMaxAgeSeconds)
            {
                return false;
            }

            var baseString = $"v0:{timestamp}:{body}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));

            var computed = new StringBuilder("v0=", 3 + (hash.Length * 2));
            foreach (var value in hash)
            {
                computed.Append(value.ToString("x2"));
            }

            var expectedBytes = Encoding.UTF8.GetBytes(computed.ToString());
            var actualBytes = Encoding.UTF8.GetBytes(signature);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
    }
}
