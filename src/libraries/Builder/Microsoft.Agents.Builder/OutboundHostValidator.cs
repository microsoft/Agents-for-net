// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Validates that an outbound URL targets an allowed host before the SDK makes a
    /// server-side, often token-bearing, request to it (e.g. Activity.ServiceUrl callbacks
    /// or attachment downloads). This is the SDK's shared anti-SSRF ("allowed hosts") control.
    /// </summary>
    public interface IOutboundHostValidator
    {
        /// <summary>
        /// Gets a value indicating whether enforcement is enabled. When <see langword="false"/>,
        /// <see cref="Microsoft.Agents.Builder.IOutboundHostValidator.IsAllowed(System.String)"/> returns <see langword="true"/> for any input (current, un-restricted behavior).
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Determines whether an outbound request to <paramref name="url"/> is permitted.
        /// </summary>
        /// <param name="url">The absolute URL that is about to be requested.</param>
        /// <returns>
        /// <see langword="true"/> when enforcement is disabled, or when enforcement is enabled and the URL's host
        /// matches the built-in first-party allowlist or a configured host; otherwise <see langword="false"/>.
        /// </returns>
        bool IsAllowed(string url);

        /// <summary>
        /// Determines whether an outbound request to <paramref name="uri"/> is permitted.
        /// </summary>
        /// <param name="uri">The absolute URI that is about to be requested.</param>
        bool IsAllowed(Uri uri);
    }

    /// <summary>
    /// Options controlling the shared <see cref="Microsoft.Agents.Builder.IOutboundHostValidator"/> "allowed hosts" anti-SSRF control.
    /// </summary>
    /// <remarks>
    /// Enforcement is <b>opt-in</b>: <see cref="Microsoft.Agents.Builder.OutboundHostValidatorOptions.Enabled"/> defaults to <see langword="false"/> so existing behavior
    /// is preserved until an operator explicitly turns it on.
    /// </remarks>
    public class OutboundHostValidatorOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether outbound host validation is enforced. Defaults to <see langword="false"/>.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the built-in list of Microsoft first-party hosts
        /// (Bot Connector, Graph, SharePoint, Azure Blob/AMS) is included when enforcement is enabled.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool IncludeDefaultMicrosoftHosts { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether allowlisted hosts may target private, loopback, link-local,
        /// or unique-local network addresses. Defaults to <see langword="false"/>.
        /// </summary>
        public bool AllowPrivateNetworkAddresses { get; set; } = false;

        /// <summary>
        /// Gets or sets the additional allowed host suffixes. An entry matches a request host when the host equals the
        /// entry or is a subdomain of it (e.g. <c>contoso.com</c> matches <c>contoso.com</c> and <c>files.contoso.com</c>).
        /// A leading <c>*.</c> is accepted and ignored (treated as a suffix).
        /// </summary>
        public IList<string> Hosts { get; set; } = new List<string>();
    }

    /// <summary>
    /// Default <see cref="Microsoft.Agents.Builder.IOutboundHostValidator"/> implementation backed by <see cref="Microsoft.Agents.Builder.OutboundHostValidatorOptions"/>.
    /// </summary>
    public sealed class OutboundHostValidator : IOutboundHostValidator
    {
        // Built-in Microsoft first-party host suffixes used for channel callbacks and attachment downloads.
        private static readonly string[] DefaultMicrosoftHosts =
        {
            "botframework.com",           // Bot Connector / channel service URLs
            "smba.trafficmanager.net",    // Teams service URLs (exact host; trafficmanager.net is a shared namespace)
            "teams.microsoft.com",
            "teams.microsoft.us",
            "graph.microsoft.com",      // Microsoft Graph
            "sharepoint.com",           // SharePoint / OneDrive hosted attachments
            "svc.ms",                   // Teams attachment CDN (*.svc.ms)
            "blob.core.windows.net",    // Azure Blob Storage / Attachment Management Service
        };

        private readonly bool _enabled;
        private readonly bool _allowPrivateNetworkAddresses;
        private readonly IHostAddressResolver _hostAddressResolver;
        private readonly string[] _suffixes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Builder.OutboundHostValidator"/> class.
        /// </summary>
        /// <param name="options">The allowed-hosts options. When <see langword="null"/>, enforcement is disabled.</param>
        public OutboundHostValidator(OutboundHostValidatorOptions options)
            : this(options, new DnsHostAddressResolver())
        {
        }

        internal OutboundHostValidator(OutboundHostValidatorOptions options, IHostAddressResolver hostAddressResolver)
        {
            options ??= new OutboundHostValidatorOptions();
            _enabled = options.Enabled;
            _allowPrivateNetworkAddresses = options.AllowPrivateNetworkAddresses;
            _hostAddressResolver = hostAddressResolver ?? throw new ArgumentNullException(nameof(hostAddressResolver));

            var suffixes = new List<string>();
            if (options.IncludeDefaultMicrosoftHosts)
            {
                suffixes.AddRange(DefaultMicrosoftHosts);
            }

            if (options.Hosts != null)
            {
                foreach (var host in options.Hosts)
                {
                    var normalized = Normalize(host);
                    if (normalized != null)
                    {
                        suffixes.Add(normalized);
                    }
                }
            }

            _suffixes = suffixes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <inheritdoc/>
        public bool Enabled => _enabled;

        /// <inheritdoc/>
        public bool IsAllowed(string url)
        {
            if (!_enabled)
            {
                return true;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsAllowed(uri);
        }

        /// <inheritdoc/>
        public bool IsAllowed(Uri uri)
        {
            if (!_enabled)
            {
                return true;
            }

            if (uri == null || !uri.IsAbsoluteUri)
            {
                return false;
            }

            if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            var host = uri.Host;
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            foreach (var suffix in _suffixes)
            {
                if (string.Equals(host, suffix, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_allowPrivateNetworkAddresses
                        && (uri.IsLoopback
                            || (IPAddress.TryParse(uri.IdnHost, out var address)
                                && OutboundHostAddressValidation.IsBlockedAddress(address))))
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        internal async Task<bool> IsAllowedAsync(
            Uri uri,
            CancellationToken cancellationToken,
            IHostAddressResolver hostAddressResolver = null)
        {
            hostAddressResolver ??= _hostAddressResolver;

            if (!_enabled)
            {
                return true;
            }

            if (!IsAllowed(uri))
            {
                return false;
            }

            if (_allowPrivateNetworkAddresses || IPAddress.TryParse(uri.IdnHost, out _))
            {
                return true;
            }

            return await OutboundHostAddressValidation.AreAddressesAllowedAsync(
                uri.IdnHost,
                hostAddressResolver,
                cancellationToken).ConfigureAwait(false);
        }

        private static string Normalize(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            host = host.Trim();

            if (host.StartsWith("*.", StringComparison.Ordinal))
            {
                host = host.Substring(2);
            }

            // Tolerate operators pasting a full URL (e.g. "https://contoso.com/path") or a
            // "host:port"/"host/path" value: extract just the host so it can match Uri.Host.
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
            }
            else
            {
                var slash = host.IndexOf('/');
                if (slash >= 0)
                {
                    host = host.Substring(0, slash);
                }

                var colon = host.IndexOf(':');
                if (colon >= 0)
                {
                    host = host.Substring(0, colon);
                }
            }

            return string.IsNullOrWhiteSpace(host) ? null : host;
        }
    }

    internal interface IHostAddressResolver
    {
        Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken);
    }

    internal sealed class DnsHostAddressResolver : IHostAddressResolver
    {
        public async Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            return await Dns.GetHostAddressesAsync(hostNameOrAddress, cancellationToken).ConfigureAwait(false);
#else
            cancellationToken.ThrowIfCancellationRequested();
            var addresses = await Dns.GetHostAddressesAsync(hostNameOrAddress).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return addresses;
#endif
        }
    }

    internal static class OutboundHostAddressValidation
    {
        public static async Task<bool> AreAddressesAllowedAsync(
            string host,
            IHostAddressResolver hostAddressResolver,
            CancellationToken cancellationToken)
        {
            IPAddress[] addresses;
            try
            {
                addresses = await hostAddressResolver.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            return addresses.Length > 0 && addresses.All(address => !IsBlockedAddress(address));
        }

        public static bool IsBlockedAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.Any.Equals(address)
                || IPAddress.IPv6Any.Equals(address)
                || IPAddress.IsLoopback(address))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254);
            }

            return address.AddressFamily == AddressFamily.InterNetworkV6
                && ((bytes[0] & 0xFE) == 0xFC
                    || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80));
        }
    }
}
