// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;

namespace Microsoft.Agents.Authentication.Msal.Interfaces
{
    public interface IMSALConnectionSettings : IConnectionSettings
    {
        public string ClientSecret { get; set; }

        /// <summary>
        /// Auth Type to use for the connection
        /// </summary>
        AuthTypes AuthType { get; set; }

        /// <summary>
        /// Certificate thumbprint to use for the connection when using a certificate that is resident on the machine
        /// </summary>
        string CertificateThumbPrint { get; set; }

        /// <summary>
        /// Subject name to search a cert for. 
        /// </summary>
        string CertificateSubjectName { get; set; }

        /// <summary>
        /// Cert store name to use. 
        /// </summary>
        string CertificateStoreName { get; set; }

        /// <summary>
        /// Only use valid certs.  Defaults to true.
        /// </summary>
        public bool ValidCertificateOnly { get; set; }

        /// <summary>
        /// Use x5c for certs.  Defaults to false.
        /// </summary>
        public bool SendX5C { get; set; }

        /// <summary>
        /// ClientId of the ManagedIdentity used with FederatedCredentials
        /// </summary>
        public string FederatedClientId { get; set; }

        /// <summary>
        /// Token path used for the workload identity, like the MSAL example for AKS, equal to AZURE_FEDERATED_TOKEN_FILE. 
        /// </summary>
        public string FederatedTokenFile { get; set; }
    }
}