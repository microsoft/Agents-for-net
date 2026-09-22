// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class OAuthAuthorizationServerMetadataClientTests
{
    [Fact]
    public void CreateInnerHandler_DisablesAutomaticRedirects()
    {
        HttpMessageHandler handler = OAuthAuthorizationServerMetadataClient.CreateInnerHandler();

        HttpClientHandler httpClientHandler = Assert.IsType<HttpClientHandler>(handler);
        Assert.False(httpClientHandler.AllowAutoRedirect);
    }

    [Fact]
    public void GetMetadataCandidatesFromMetadataUrl_UsesConfiguredUrlAsSupplied()
    {
        Uri metadataUrl = new("https://identity.example.com/custom/.well-known/oauth-authorization-server");

        IReadOnlyList<Uri> candidates = OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromMetadataUrl(metadataUrl);

        Assert.Equal([metadataUrl], candidates);
    }

    [Fact]
    public void GetMetadataCandidatesFromOrigin_ReturnsAuthorizationServerBeforeOpenId()
    {
        Uri tokenEndpoint = new("https://identity.example.com/oauth/token");

        IReadOnlyList<Uri> candidates = OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromOrigin(tokenEndpoint);

        Assert.Equal(
        [
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server"),
            new Uri("https://identity.example.com/.well-known/openid-configuration"),
        ],
        candidates);
    }

    [Fact]
    public void GetMetadataCandidatesFromIssuer_PreservesPathWhenBuildingWellKnownCandidates()
    {
        Uri issuer = new("https://identity.example.com/tenant/v2.0");

        IReadOnlyList<Uri> candidates = OAuthAuthorizationServerMetadataClient.GetMetadataCandidatesFromIssuer(issuer);

        Assert.Equal(
        [
            new Uri("https://identity.example.com/.well-known/oauth-authorization-server/tenant/v2.0"),
            new Uri("https://identity.example.com/tenant/v2.0/.well-known/openid-configuration"),
        ],
        candidates);
    }

    [Fact]
    public void DeduplicateMetadataCandidates_PreservesFirstSeenOrder()
    {
        Uri first = new("https://identity.example.com/.well-known/oauth-authorization-server");
        Uri second = new("https://identity.example.com/.well-known/openid-configuration");

        IReadOnlyList<Uri> candidates = OAuthAuthorizationServerMetadataClient.DeduplicateMetadataCandidates(
        [
            first,
            second,
            first,
            second,
        ]);

        Assert.Equal([first, second], candidates);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsFirstValidCandidateAfterSkippingFailures()
    {
        Uri first = new("https://identity.example.com/.well-known/oauth-authorization-server");
        Uri second = new("https://identity.example.com/.well-known/openid-configuration");
        Uri third = new("https://identity.example.com/.well-known/oauth-authorization-server/tenant");
        var handler = new StubMetadataHandler(
            CreateResponse(HttpStatusCode.InternalServerError, """{ "error": "temporary" }"""),
            CreateResponse(HttpStatusCode.OK, "{ this is not json"),
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com/tenant",
                    authorizationEndpoint: "https://identity.example.com/tenant/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/tenant/oauth/token",
                    registrationEndpoint: "https://identity.example.com/tenant/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        OAuthAuthorizationServerMetadata metadata = await sut.DiscoverAsync(
            [first, second, third],
            CancellationToken.None);

        Assert.Equal(new Uri("https://identity.example.com/tenant"), metadata.Issuer);
        Assert.Equal(third, metadata.MetadataUrl);
        Assert.Equal(new Uri("https://identity.example.com/tenant/oauth/authorize"), metadata.AuthorizationEndpoint);
        Assert.Equal(new Uri("https://identity.example.com/tenant/oauth/token"), metadata.TokenEndpoint);
        Assert.Equal(new Uri("https://identity.example.com/tenant/oauth/register"), metadata.RegistrationEndpoint);
        Assert.Equal(["authorization_code", "refresh_token"], metadata.GrantTypesSupported);
        Assert.Equal(["S256", "plain"], metadata.CodeChallengeMethodsSupported);
        Assert.Equal([first, second, third], handler.RequestUris);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsRedirectCandidateWithoutFollowingLocation()
    {
        Uri redirectingCandidate = new("https://identity.example.com/.well-known/oauth-authorization-server");
        Uri redirectedTarget = new("https://login.example.com/.well-known/oauth-authorization-server");
        Uri fallbackCandidate = new("https://identity.example.com/.well-known/openid-configuration");
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers =
            {
                Location = redirectedTarget,
            },
        };
        var handler = new StubMetadataHandler(
            redirectResponse,
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com",
                    authorizationEndpoint: "https://identity.example.com/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/oauth/token",
                    registrationEndpoint: "https://identity.example.com/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        OAuthAuthorizationServerMetadata metadata = await sut.DiscoverAsync(
            [redirectingCandidate, fallbackCandidate],
            CancellationToken.None);

        Assert.Equal(fallbackCandidate, metadata.MetadataUrl);
        Assert.Equal([redirectingCandidate, fallbackCandidate], handler.RequestUris);
        Assert.DoesNotContain(redirectedTarget, handler.RequestUris);
    }

    [Theory]
    [MemberData(nameof(GetInvalidMetadataPayloads))]
    public async Task DiscoverAsync_RejectsInvalidMetadata(string payload)
    {
        Uri candidate = new("https://identity.example.com/.well-known/oauth-authorization-server");
        var handler = new StubMetadataHandler(CreateResponse(HttpStatusCode.OK, payload));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DiscoverAsync([candidate], CancellationToken.None));

        Assert.Contains(candidate.AbsoluteUri, exception.Message, StringComparison.Ordinal);
        Assert.Equal([candidate], handler.RequestUris);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsWellKnownCandidateWhoseIssuerDoesNotMatchDerivedPath()
    {
        Uri candidate = new("https://identity.example.com/.well-known/oauth-authorization-server/tenant");
        var handler = new StubMetadataHandler(
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com/other",
                    authorizationEndpoint: "https://identity.example.com/tenant/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/tenant/oauth/token",
                    registrationEndpoint: "https://identity.example.com/tenant/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DiscoverAsync([candidate], CancellationToken.None));

        Assert.Contains(candidate.AbsoluteUri, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_TrailingSlashNearMatchDoesNotInvertIssuer()
    {
        Uri candidate = new("https://identity.example.com/.well-known/openid-configuration/");
        var handler = new StubMetadataHandler(
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com/tenant",
                    authorizationEndpoint: "https://identity.example.com/tenant/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/tenant/oauth/token",
                    registrationEndpoint: "https://identity.example.com/tenant/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        OAuthAuthorizationServerMetadata metadata = await sut.DiscoverAsync([candidate], CancellationToken.None);

        Assert.Equal(new Uri("https://identity.example.com/tenant"), metadata.Issuer);
        Assert.Equal(candidate, metadata.MetadataUrl);
    }

    [Fact]
    public async Task DiscoverAsync_CasingVariantNearMatchDoesNotInvertIssuer()
    {
        Uri candidate = new("https://identity.example.com/.WELL-known/openid-configuration");
        var handler = new StubMetadataHandler(
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com/tenant",
                    authorizationEndpoint: "https://identity.example.com/tenant/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/tenant/oauth/token",
                    registrationEndpoint: "https://identity.example.com/tenant/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        OAuthAuthorizationServerMetadata metadata = await sut.DiscoverAsync([candidate], CancellationToken.None);

        Assert.Equal(new Uri("https://identity.example.com/tenant"), metadata.Issuer);
        Assert.Equal(candidate, metadata.MetadataUrl);
    }

    [Fact]
    public async Task DiscoverAsync_Rfc8414TrailingSlashNearMatchDoesNotInvertIssuer()
    {
        Uri candidate = new("https://identity.example.com/.well-known/oauth-authorization-server/");
        var handler = new StubMetadataHandler(
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com/tenant",
                    authorizationEndpoint: "https://identity.example.com/tenant/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/tenant/oauth/token",
                    registrationEndpoint: "https://identity.example.com/tenant/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        OAuthAuthorizationServerMetadata metadata = await sut.DiscoverAsync([candidate], CancellationToken.None);

        Assert.Equal(new Uri("https://identity.example.com/tenant"), metadata.Issuer);
        Assert.Equal(candidate, metadata.MetadataUrl);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsCanonicalOpenIdCandidateWhoseIssuerDoesNotMatchDerivedPath()
    {
        Uri candidate = new("https://identity.example.com/tenant/.well-known/openid-configuration");
        var handler = new StubMetadataHandler(
            CreateResponse(
                HttpStatusCode.OK,
                CreateMetadataJson(
                    issuer: "https://identity.example.com/other",
                    authorizationEndpoint: "https://identity.example.com/tenant/oauth/authorize",
                    tokenEndpoint: "https://identity.example.com/tenant/oauth/token",
                    registrationEndpoint: "https://identity.example.com/tenant/oauth/register")));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DiscoverAsync([candidate], CancellationToken.None));

        Assert.Contains(candidate.AbsoluteUri, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_FinalFailureListsAttemptedUrlsWithoutResponseBodies()
    {
        Uri first = new("https://identity.example.com/.well-known/oauth-authorization-server");
        Uri second = new("https://identity.example.com/.well-known/openid-configuration");
        var handler = new StubMetadataHandler(
            CreateResponse(HttpStatusCode.BadRequest, """{ "error": "client_secret=super-secret" }"""),
            CreateResponse(HttpStatusCode.OK, """{ "issuer": "https://identity.example.com", "leaked": "refresh_token=top-secret" }"""));
        var sut = new OAuthAuthorizationServerMetadataClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DiscoverAsync([first, second], CancellationToken.None));

        Assert.Contains(first.AbsoluteUri, exception.Message, StringComparison.Ordinal);
        Assert.Contains(second.AbsoluteUri, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret=super-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh_token=top-secret", exception.Message, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> GetInvalidMetadataPayloads()
    {
        yield return [CreateMetadataJson(issuer: "http://identity.example.com")];
        yield return [CreateMetadataJson(authorizationEndpoint: "http://identity.example.com/oauth/authorize")];
        yield return [CreateMetadataJson(tokenEndpoint: "http://identity.example.com/oauth/token")];
        yield return [CreateMetadataJson(registrationEndpoint: "http://identity.example.com/oauth/register")];
        yield return [CreateMetadataJson(issuer: "https://other.example.com")];
        yield return [CreateMetadataJson(tokenEndpoint: null)];
        yield return [CreateMetadataJson(registrationEndpoint: null)];
        yield return [CreateMetadataJson(grantTypesSupported: ["client_credentials"])];
        yield return [CreateMetadataJson(codeChallengeMethodsSupported: ["plain"])];
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static string CreateMetadataJson(
        string issuer = "https://identity.example.com",
        string? authorizationEndpoint = "https://identity.example.com/oauth/authorize",
        string? tokenEndpoint = "https://identity.example.com/oauth/token",
        string? registrationEndpoint = "https://identity.example.com/oauth/register",
        string[]? grantTypesSupported = null,
        string[]? codeChallengeMethodsSupported = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["issuer"] = issuer,
            ["token_endpoint"] = tokenEndpoint,
            ["registration_endpoint"] = registrationEndpoint,
            ["grant_types_supported"] = grantTypesSupported ?? ["authorization_code", "refresh_token"],
            ["code_challenge_methods_supported"] = codeChallengeMethodsSupported ?? ["S256", "plain"],
        };
        if (authorizationEndpoint is not null)
        {
            payload["authorization_endpoint"] = authorizationEndpoint;
        }

        return JsonSerializer.Serialize(payload);
    }

    private sealed class StubMetadataHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.NotNull(request.RequestUri);
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
