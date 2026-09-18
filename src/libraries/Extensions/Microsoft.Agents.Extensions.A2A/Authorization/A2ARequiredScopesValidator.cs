// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.A2A.Errors;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

internal static class A2ARequiredScopesValidator
{
    internal static void Validate(
        string handlerName,
        string token,
        IEnumerable<string> requiredScopes)
    {
        JwtSecurityToken jwt;
        try
        {
            jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AuthorizationDelegatedJwtRequired,
                exception,
                handlerName);
        }

        string identityType = jwt.Claims.FirstOrDefault(
            claim => claim.Type == "idtyp")?.Value;
        string[] grantedScopes = jwt.Claims
            .Where(claim => claim.Type == "scp")
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (string.Equals(identityType, "app", StringComparison.Ordinal)
            || grantedScopes.Length == 0)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AuthorizationDelegatedJwtRequired,
                null,
                handlerName);
        }

        string[] missingScopes = requiredScopes
            .Where(requiredScope => !IsGranted(requiredScope, grantedScopes))
            .ToArray();
        if (missingScopes.Length > 0)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AuthorizationRequiredScopesNotGranted,
                null,
                handlerName,
                string.Join(", ", missingScopes));
        }
    }

    private static bool IsGranted(string requiredScope, string[] grantedScopes)
    {
        if (grantedScopes.Contains(requiredScope, StringComparer.Ordinal))
        {
            return true;
        }

        string normalizedScope = requiredScope.TrimEnd('/');
        int separator = normalizedScope.LastIndexOf('/');
        string scopeValue = separator >= 0
            ? normalizedScope[(separator + 1)..]
            : normalizedScope;
        return scopeValue.Length > 0
            && grantedScopes.Contains(scopeValue, StringComparer.Ordinal);
    }
}
