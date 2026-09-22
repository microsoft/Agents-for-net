// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed record OAuthAuthorizationServerMetadata(
    Uri Issuer,
    Uri MetadataUrl,
    Uri? AuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri RegistrationEndpoint,
    IReadOnlyList<string> GrantTypesSupported,
    IReadOnlyList<string> CodeChallengeMethodsSupported);
