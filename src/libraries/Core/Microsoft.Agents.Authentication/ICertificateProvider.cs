// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Agents.Authentication
{
    public interface ICertificateProvider
    {
        X509Certificate2 GetCertificate();        
    }
}
