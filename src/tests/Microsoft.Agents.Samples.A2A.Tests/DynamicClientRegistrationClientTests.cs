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

public class DynamicClientRegistrationClientTests
{
    [Fact]
    public void CreateInnerHandler_DisablesAutomaticRedirects()
    {
        HttpMessageHandler handler = DynamicClientRegistrationClient.CreateInnerHandler();

        HttpClientHandler httpClientHandler = Assert.IsType<HttpClientHandler>(handler);
        Assert.False(httpClientHandler.AllowAutoRedirect);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_SendsExpectedPublicClientRegistrationRequest()
    {
        Uri registrationEndpoint = new("https://identity.example.com/oauth/register");
        Uri redirectUri = new("http://localhost:8400/callback/");
        var handler = new RecordingRegistrationHandler(
            CreateResponse(
                HttpStatusCode.Created,
                """
                {
                  "client_id": "provider-client-id",
                  "redirect_uris": ["http://localhost:8400/callback/"]
                }
                """));
        var sut = new DynamicClientRegistrationClient(handler);

        OAuthClientRegistration registration = await sut.RegisterPublicClientAsync(
            "browser",
            registrationEndpoint,
            redirectUri,
            CancellationToken.None);

        Assert.Equal("browser", registration.Id);
        Assert.Equal([A2AOAuthFlowType.AuthorizationCode], registration.GrantTypes);
        Assert.Equal("provider-client-id", registration.ClientId);
        Assert.Null(registration.ClientSecret);
        Assert.Equal(redirectUri, registration.RedirectUri);
        Assert.Equal(OAuthTokenEndpointAuthenticationMethod.None, registration.TokenEndpointAuthenticationMethod);
        Assert.True(registration.UsePkce);

        Assert.Equal(registrationEndpoint, handler.RequestUri);
        Assert.Equal("application/json", handler.ContentType);

        using JsonDocument json = JsonDocument.Parse(handler.Content);
        Assert.Equal(4, json.RootElement.EnumerateObject().Count());
        Assert.Equal(["http://localhost:8400/callback/"], ReadStringArray(json.RootElement.GetProperty("redirect_uris")));
        Assert.Equal(["authorization_code"], ReadStringArray(json.RootElement.GetProperty("grant_types")));
        Assert.Equal(["code"], ReadStringArray(json.RootElement.GetProperty("response_types")));
        Assert.Equal("none", json.RootElement.GetProperty("token_endpoint_auth_method").GetString());
    }

    [Fact]
    public async Task RegisterPublicClientAsync_RejectsNonHttpsRegistrationEndpointBeforeSendingRequest()
    {
        var handler = new RecordingRegistrationHandler(CreateResponse(HttpStatusCode.Created, """{ "client_id": "provider-client-id" }"""));
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                new Uri("http://identity.example.com/oauth/register"),
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("registration endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_RejectsRedirectResponseWithoutFollowingLocation()
    {
        Uri registrationEndpoint = new("https://identity.example.com/oauth/register");
        Uri redirectedTarget = new("https://login.example.com/oauth/register");
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = redirectedTarget;
        var handler = new RecordingRegistrationHandler(response);
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                registrationEndpoint,
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("302", exception.Message, StringComparison.Ordinal);
        Assert.Equal([registrationEndpoint], handler.RequestUris);
        Assert.DoesNotContain(redirectedTarget, handler.RequestUris);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_RejectsSuccessfulResponseFromDifferentFinalOriginWithoutLeakingBody()
    {
        Uri registrationEndpoint = new("https://identity.example.com/oauth/register");
        Uri unexpectedFinalUri = new("https://login.example.com/oauth/register");
        var response = CreateResponse(
            HttpStatusCode.OK,
            """
            {
              "client_id": "provider-client-id",
              "client_secret": "top-secret"
            }
            """);
        var handler = new RecordingRegistrationHandler(response)
        {
            ResponseRequestUri = unexpectedFinalUri,
        };
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                registrationEndpoint,
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("final request URI", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(unexpectedFinalUri.AbsoluteUri, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_NonSuccessResponse_IncludesOnlySafeOAuthErrorFields()
    {
        var handler = new RecordingRegistrationHandler(
            CreateResponse(
                HttpStatusCode.BadRequest,
                """
                {
                  "error": "invalid_redirect_uri",
                  "error_description": "Redirect URI is not permitted.",
                  "client_secret": "top-secret",
                  "details": "refresh_token=super-secret"
                }
                """));
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                new Uri("https://identity.example.com/oauth/register"),
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("invalid_redirect_uri", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Redirect URI is not permitted.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh_token=super-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_MissingClientId_Throws()
    {
        var handler = new RecordingRegistrationHandler(
            CreateResponse(
                HttpStatusCode.Created,
                """
                {
                  "redirect_uris": ["http://localhost:8400/callback/"]
                }
                """));
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                new Uri("https://identity.example.com/oauth/register"),
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("client_id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_ReturnedSecret_IsRejected()
    {
        var handler = new RecordingRegistrationHandler(
            CreateResponse(
                HttpStatusCode.Created,
                """
                {
                  "client_id": "provider-client-id",
                  "client_secret": "top-secret"
                }
                """));
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                new Uri("https://identity.example.com/oauth/register"),
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("client_secret", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("client_secret_post")]
    [InlineData("client_secret_basic")]
    public async Task RegisterPublicClientAsync_NonNoneTokenEndpointAuthenticationMethod_IsRejected(string authMethod)
    {
        var handler = new RecordingRegistrationHandler(
            CreateResponse(
                HttpStatusCode.Created,
                $$"""
                {
                  "client_id": "provider-client-id",
                  "token_endpoint_auth_method": "{{authMethod}}"
                }
                """));
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                new Uri("https://identity.example.com/oauth/register"),
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("token_endpoint_auth_method", exception.Message, StringComparison.Ordinal);
        Assert.Contains(authMethod, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPublicClientAsync_ReturnedRedirectUrisMustIncludeRequestedRedirectUri()
    {
        var handler = new RecordingRegistrationHandler(
            CreateResponse(
                HttpStatusCode.Created,
                """
                {
                  "client_id": "provider-client-id",
                  "redirect_uris": ["http://localhost:8400/other/"]
                }
                """));
        var sut = new DynamicClientRegistrationClient(handler);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterPublicClientAsync(
                "browser",
                new Uri("https://identity.example.com/oauth/register"),
                new Uri("http://localhost:8400/callback/"),
                CancellationToken.None));

        Assert.Contains("redirect_uris", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InMemoryStore_SaveAndGet_UsesOrdinalProviderIdentityAndNormalizedRedirectUri()
    {
        IOAuthClientRegistrationStore sut = new InMemoryOAuthClientRegistrationStore();
        OAuthClientRegistration expected = CreateRegistration();

        await sut.SaveAsync(
            "provider",
            new Uri("http://LOCALHOST:8400/callback/"),
            expected,
            CancellationToken.None);

        OAuthClientRegistration? resolved = await sut.GetAsync(
            "provider",
            new Uri("http://localhost:8400/callback/"),
            CancellationToken.None);
        OAuthClientRegistration? differentProvider = await sut.GetAsync(
            "Provider",
            new Uri("http://localhost:8400/callback/"),
            CancellationToken.None);

        Assert.Equal(expected, resolved);
        Assert.Null(differentProvider);
    }

    [Fact]
    public async Task InMemoryStore_CanceledSave_DoesNotPersistRegistration()
    {
        IOAuthClientRegistrationStore sut = new InMemoryOAuthClientRegistrationStore();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.SaveAsync(
                "provider",
                new Uri("http://localhost:8400/callback/"),
                CreateRegistration(),
                cancellationTokenSource.Token));

        OAuthClientRegistration? registration = await sut.GetAsync(
            "provider",
            new Uri("http://localhost:8400/callback/"),
            CancellationToken.None);

        Assert.Null(registration);
    }

    [Fact]
    public async Task InMemoryStore_CanceledGet_ThrowsBeforeRead()
    {
        IOAuthClientRegistrationStore sut = new InMemoryOAuthClientRegistrationStore();
        await sut.SaveAsync(
            "provider",
            new Uri("http://localhost:8400/callback/"),
            CreateRegistration(),
            CancellationToken.None);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.GetAsync(
                "provider",
                new Uri("http://localhost:8400/callback/"),
                cancellationTokenSource.Token));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static string[] ReadStringArray(JsonElement array)
    {
        var values = new List<string>();
        foreach (JsonElement item in array.EnumerateArray())
        {
            values.Add(item.GetString()!);
        }

        return [.. values];
    }

    private static OAuthClientRegistration CreateRegistration()
        => new(
            "browser",
            [A2AOAuthFlowType.AuthorizationCode],
            "provider-client-id",
            ClientSecret: null,
            RedirectUri: new Uri("http://localhost:8400/callback/"),
            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
            UsePkce: true);

    private sealed class RecordingRegistrationHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public string Content { get; private set; } = string.Empty;

        public string? ContentType { get; private set; }

        public Uri? RequestUri { get; private set; }

        public List<Uri> RequestUris { get; } = [];

        public Uri? ResponseRequestUri { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Assert.NotNull(request.RequestUri);
            RequestUris.Add(request.RequestUri!);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            HttpResponseMessage response = _responses.Dequeue();
            response.RequestMessage = new HttpRequestMessage(request.Method, ResponseRequestUri ?? request.RequestUri);
            return response;
        }
    }
}
