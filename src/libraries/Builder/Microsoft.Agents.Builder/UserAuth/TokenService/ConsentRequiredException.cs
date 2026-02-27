// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Represents an exception that is thrown when user consent is required to proceed with the authorization flow.
    /// </summary>
    public class ConsentRequiredException : Exception
    {
    }
}
