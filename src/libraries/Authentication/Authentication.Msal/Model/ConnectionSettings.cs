// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Interfaces;
using Microsoft.Agents.Core;
using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.Authentication.Msal.Model
{
    /// <summary>
    /// Gets and manages connection settings for MSAL Auth
    /// </summary>
    public class ConnectionSettings : ConnectionSettingsBase, IMSALConnectionSettings
    {
        public ConnectionSettings() : base() { }

        public ConnectionSettings(IConfigurationSection msalConfigurationSection) : base(msalConfigurationSection)
        {
            AssertionHelpers.ThrowIfNull(nameof(msalConfigurationSection), nameof(msalConfigurationSection));

            if (msalConfigurationSection != null)
            {
                CertificateThumbPrint = msalConfigurationSection.GetValue<string>("CertThumbprint", string.Empty);
                CertificateSubjectName = msalConfigurationSection.GetValue<string>("CertSubjectName", string.Empty);
                CertificateStoreName = msalConfigurationSection.GetValue<string>("CertStoreName", "My");
                ValidCertificateOnly = msalConfigurationSection.GetValue<bool>("ValidCertificateOnly", true);
                SendX5C = msalConfigurationSection.GetValue<bool>("SendX5C", false);
                ClientSecret = msalConfigurationSection.GetValue<string>("ClientSecret", string.Empty);
                AuthType = msalConfigurationSection.GetValue<AuthTypes>("AuthType", AuthTypes.ClientSecret);
                FederatedClientId = msalConfigurationSection.GetValue<string>("FederatedClientId", string.Empty);
            }

            ValidateConfiguration();
        }

        /// <summary>
        /// Auth Type to use for the connection
        /// </summary>
        public AuthTypes AuthType { get; set; } = AuthTypes.ClientSecret;

        /// <summary>
        /// Certificate thumbprint to use for the connection when using a certificate that is resident on the machine
        /// </summary>
        public string CertificateThumbPrint { get; set; }

        /// <summary>
        /// Client Secret to use for the connection when using a client secret
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Subject name to search a cert for. 
        /// </summary>
        public string CertificateSubjectName { get; set; }

        /// <summary>
        /// Cert store name to use. 
        /// </summary>
        public string CertificateStoreName { get; set; }

        /// <summary>
        /// Only use valid certs.  Defaults to true.
        /// </summary>
        public bool ValidCertificateOnly { get; set; } = true;

        /// <summary>
        /// Use x5c for certs.  Defaults to false.
        /// </summary>
        public bool SendX5C { get; set; } = false;

        /// <summary>
        /// ClientId of the ManagedIdentity used with FederatedCredentials
        /// </summary>
        public string FederatedClientId { get; set; }

        /// <summary>
        /// Validates required properties are present in the configuration for the requested authentication type. 
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public void ValidateConfiguration()
        {
            switch (AuthType)
            {
                case AuthTypes.Certificate:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(CertificateThumbPrint))
                    {
                        throw new ArgumentNullException(nameof(CertificateThumbPrint), "CertificateThumbPrint is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.CertificateSubjectName:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(CertificateSubjectName))
                    {
                        throw new ArgumentNullException(nameof(CertificateSubjectName), "CertificateSubjectName is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.ClientSecret:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(ClientSecret))
                    {
                        throw new ArgumentNullException(nameof(ClientSecret), "ClientSecret is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.UserManagedIdentity:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    break;
                case AuthTypes.SystemManagedIdentity:
                    // No additional validation needed
                    break;
                case AuthTypes.FederatedCredentials:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(FederatedClientId))
                    {
                        throw new ArgumentNullException(nameof(FederatedClientId), "FederatedClientId is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                default:
                    break;
            }
        }

    }
}
