// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using System;

namespace A2AAgent;

internal static class BearerTokenSchemeSelector
{
    public static string Select(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return A2AAgentAuthenticationDefaults.GitHubScheme;
        }

        string token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return A2AAgentAuthenticationDefaults.GitHubScheme;
        }

        return IsJwtShaped(token)
            ? JwtBearerDefaults.AuthenticationScheme
            : A2AAgentAuthenticationDefaults.GitHubScheme;
    }

    private static bool IsJwtShaped(string token)
    {
        ReadOnlySpan<char> remaining = token.AsSpan();

        for (int segment = 0; segment < 3; segment++)
        {
            int separatorIndex = remaining.IndexOf('.');
            ReadOnlySpan<char> currentSegment = segment < 2
                ? separatorIndex >= 0 ? remaining[..separatorIndex] : []
                : separatorIndex >= 0 ? [] : remaining;

            if (currentSegment.IsEmpty || !IsBase64Url(currentSegment))
            {
                return false;
            }

            if (segment < 2)
            {
                if (separatorIndex < 0)
                {
                    return false;
                }

                remaining = remaining[(separatorIndex + 1)..];
            }
        }

        return true;
    }

    private static bool IsBase64Url(ReadOnlySpan<char> segment)
    {
        foreach (char character in segment)
        {
            if ((character is < 'A' or > 'Z')
                && (character is < 'a' or > 'z')
                && (character is < '0' or > '9')
                && character != '-'
                && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
