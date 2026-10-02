// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class OutboundHostValidatorTests
    {
        [Theory]
        [InlineData("https://evil.example.com/relay")]
        [InlineData("https://169.254.169.254/latest/meta-data")]
        [InlineData("http://localhost/admin")]
        [InlineData("not-a-uri")]
        [InlineData(null)]
        public void Disabled_AllowsEverything(string url)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions { Enabled = false });

            Assert.False(validator.Enabled);
            Assert.True(validator.IsAllowed(url));
        }

        [Fact]
        public void NullOptions_DisablesEnforcement()
        {
            var validator = new OutboundHostValidator(null);

            Assert.False(validator.Enabled);
            Assert.True(validator.IsAllowed("https://evil.example.com/relay"));
        }

        [Theory]
        [InlineData("https://smba.trafficmanager.net/teams/")]
        [InlineData("https://graph.microsoft.com/v1.0/me")]
        [InlineData("https://contoso.sharepoint.com/file")]
        [InlineData("https://foo.svc.ms/download")]
        [InlineData("https://account.blob.core.windows.net/container/blob")]
        [InlineData("https://webchat.botframework.com/callback")]
        public void Enabled_AllowsFirstPartyMicrosoftHosts(string url)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions { Enabled = true });

            Assert.True(validator.Enabled);
            Assert.True(validator.IsAllowed(url));
        }

        [Theory]
        [InlineData("https://evil.example.com/relay")]
        [InlineData("https://169.254.169.254/latest/meta-data")]
        [InlineData("https://internal-test.local:8443/secret")]
        [InlineData("http://localhost/admin")]
        [InlineData("https://localhost/admin")]
        [InlineData("https://evil.trafficmanager.net/relay")]
        public void Enabled_DeniesUnknownHosts(string url)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions { Enabled = true });

            Assert.False(validator.IsAllowed(url));
        }

        [Theory]
        [InlineData("https://contoso.com")]
        [InlineData("https://contoso.com/some/path")]
        [InlineData("contoso.com:8443")]
        [InlineData("contoso.com/path")]
        public void Enabled_NormalizesConfiguredHost_FromFullUrlOrHostPort(string configured)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { configured }
            });

            Assert.True(validator.IsAllowed("https://contoso.com/api"));
            Assert.True(validator.IsAllowed("https://files.contoso.com/api"));
        }

        [Fact]
        public void Enabled_AllowsConfiguredHost_ExactAndSubdomain()
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            Assert.True(validator.IsAllowed("https://contoso.com/api"));
            Assert.True(validator.IsAllowed("https://files.contoso.com/api"));
            Assert.False(validator.IsAllowed("https://notcontoso.com/api"));
            Assert.False(validator.IsAllowed("https://contoso.com.evil.com/api"));
        }

        [Fact]
        public void Enabled_AcceptsWildcardPrefixInConfiguredHost()
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "*.fabrikam.com" }
            });

            Assert.True(validator.IsAllowed("https://api.fabrikam.com/x"));
            Assert.True(validator.IsAllowed("https://fabrikam.com/x"));
        }

        [Fact]
        public void Enabled_WithoutDefaults_DeniesMicrosoftHosts()
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                IncludeDefaultMicrosoftHosts = false,
                Hosts = new List<string> { "contoso.com" }
            });

            Assert.False(validator.IsAllowed("https://graph.microsoft.com/v1.0/me"));
            Assert.True(validator.IsAllowed("https://contoso.com/x"));
        }

        [Fact]
        public void Enabled_HostMatchIsCaseInsensitive()
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions { Enabled = true });

            Assert.True(validator.IsAllowed("https://GRAPH.MICROSOFT.COM/v1.0/me"));
        }

        [Theory]
        [InlineData("not-a-uri")]
        [InlineData("/relative/path")]
        [InlineData(null)]
        public void Enabled_DeniesNonAbsoluteOrInvalidUrls(string url)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions { Enabled = true });

            Assert.False(validator.IsAllowed(url));
        }

        [Theory]
        [InlineData("ftp://files.contoso.com/file.txt")]
        [InlineData("https://user@files.contoso.com/file.txt")]
        public void Enabled_DeniesUnsafeAllowedHostUris(string url)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { "contoso.com" }
            });

            Assert.False(validator.IsAllowed(url));
        }

        [Theory]
        [InlineData("http://127.0.0.1/file.txt")]
        [InlineData("http://10.0.0.1/file.txt")]
        [InlineData("http://169.254.1.1/file.txt")]
        [InlineData("http://0.0.0.0/file.txt")]
        [InlineData("http://[::1]/file.txt")]
        [InlineData("http://[::]/file.txt")]
        [InlineData("http://[fd00::1]/file.txt")]
        public void Enabled_DeniesAllowlistedPrivateNetworkAddressByDefault(string url)
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                Hosts = new List<string> { new Uri(url).Host }
            });

            Assert.False(validator.IsAllowed(url));
        }

        [Fact]
        public void Enabled_AllowsAllowlistedPrivateNetworkAddressWhenExplicitlyConfigured()
        {
            var validator = new OutboundHostValidator(new OutboundHostValidatorOptions
            {
                Enabled = true,
                AllowPrivateNetworkAddresses = true,
                Hosts = new List<string> { "127.0.0.1" }
            });

            Assert.True(validator.IsAllowed("http://127.0.0.1/file.txt"));
        }

        [Fact]
        public async Task EnabledAsync_DeniesAllowlistedHostResolvingToPrivateNetworkByDefault()
        {
            var validator = new OutboundHostValidator(
                new OutboundHostValidatorOptions
                {
                    Enabled = true,
                    Hosts = new List<string> { "files.contoso.com" }
                },
                new StubHostAddressResolver(IPAddress.Parse("10.0.0.1")));

            var result = await validator.IsAllowedAsync(
                new Uri("https://files.contoso.com/file.txt"),
                CancellationToken.None);

            Assert.False(result);
        }

        [Theory]
        [InlineData("0.0.0.0")]
        [InlineData("::")]
        public async Task EnabledAsync_DeniesHostResolvingToUnspecifiedAddress(string address)
        {
            var validator = new OutboundHostValidator(
                new OutboundHostValidatorOptions
                {
                    Enabled = true,
                    Hosts = new List<string> { "files.contoso.com" }
                },
                new StubHostAddressResolver(IPAddress.Parse(address)));

            var result = await validator.IsAllowedAsync(
                new Uri("https://files.contoso.com/file.txt"),
                CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task EnabledAsync_AllowsPrivateDnsResolutionWhenExplicitlyConfigured()
        {
            var validator = new OutboundHostValidator(
                new OutboundHostValidatorOptions
                {
                    Enabled = true,
                    AllowPrivateNetworkAddresses = true,
                    Hosts = new List<string> { "files.contoso.com" }
                },
                new StubHostAddressResolver(IPAddress.Parse("10.0.0.1")));

            var result = await validator.IsAllowedAsync(
                new Uri("https://files.contoso.com/file.txt"),
                CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task EnabledAsync_DeniesHostWhenDnsResolverRejectsTheHost()
        {
            var validator = new OutboundHostValidator(
                new OutboundHostValidatorOptions
                {
                    Enabled = true,
                    Hosts = new List<string> { "files.contoso.com" }
                },
                new ThrowingHostAddressResolver(new ArgumentException("Invalid host.")));

            var result = await validator.IsAllowedAsync(
                new Uri("https://files.contoso.com/file.txt"),
                CancellationToken.None);

            Assert.False(result);
        }

        private sealed class StubHostAddressResolver : IHostAddressResolver
        {
            private readonly IPAddress[] _addresses;

            public StubHostAddressResolver(params IPAddress[] addresses)
            {
                _addresses = addresses;
            }

            public Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken)
            {
                return Task.FromResult(_addresses);
            }
        }

        private sealed class ThrowingHostAddressResolver : IHostAddressResolver
        {
            private readonly Exception _exception;

            public ThrowingHostAddressResolver(Exception exception)
            {
                _exception = exception;
            }

            public Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken)
            {
                return Task.FromException<IPAddress[]>(_exception);
            }
        }
    }
}
