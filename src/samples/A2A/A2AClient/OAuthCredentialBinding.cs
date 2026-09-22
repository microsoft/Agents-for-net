// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed record OAuthCredentialBinding(
    string ProviderId,
    OAuthCredentialProviderType ProviderType,
    string RegistrationId,
    OAuthClientRegistration Registration,
    A2AOAuthFlowType FlowType,
    Uri? AuthorizationEndpoint,
    Uri? DeviceAuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri? MetadataUrl,
    Uri? RegistrationEndpoint,
    IReadOnlyList<string> EffectiveScopes,
    string ProviderIdentity);
