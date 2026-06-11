// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Generates PKCE (Proof Key for Code Exchange) code_verifier and code_challenge pairs
    /// for use with the Token Service sign-in flow.
    /// </summary>
    internal static class PkceHelper
    {
        private const int CodeVerifierLength = 64;

        /// <summary>
        /// Generates a cryptographically random code_verifier string.
        /// </summary>
        public static string GenerateCodeVerifier()
        {
            var bytes = new byte[CodeVerifierLength];
#if NETSTANDARD2_0
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
#else
            RandomNumberGenerator.Fill(bytes);
#endif
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        /// Computes the S256 code_challenge from a code_verifier.
        /// </summary>
        public static string ComputeCodeChallenge(string codeVerifier)
        {
#if NETSTANDARD2_0
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Base64UrlEncode(challengeBytes);
            }
#else
            var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(challengeBytes);
#endif
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
