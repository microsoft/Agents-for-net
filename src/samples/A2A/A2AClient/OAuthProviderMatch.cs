// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed record OAuthProviderMatch(
    IOAuthCredentialProvider Provider,
    string? RegistrationId,
    int ProviderSpecificity,
    int AuthoritySpecificity,
    int RegistrationSpecificity);
