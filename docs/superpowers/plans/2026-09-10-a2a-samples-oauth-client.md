# A2A Samples and OAuth Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Group the A2A samples under `src/samples/A2A`, add an interactive OAuth-capable A2A console client, and add three route-scoped request-token authorization examples to `A2AAgent`.

**Architecture:** The console client adapts the upstream `a2a-dotnet` A2ACli and supplies a shared `HttpClient` whose delegating handler obtains either no token, a delegated Agent API token through device code, or an Agent API application token through client credentials. `A2AAgent` keeps global auto-sign-in disabled and uses three `A2AUserAuthorization` handlers for delegated passthrough, delegated OBO to Graph `User.Read`, and application-token inspection.

**Tech Stack:** .NET 10, C#, `A2A` 1.0.0-preview2, Microsoft Identity Client 4.87.0, `Microsoft.Agents.Extensions.A2A`, ASP.NET Core authentication, xUnit, Moq

**Spec:** `docs/superpowers/specs/2026-09-10-a2a-samples-oauth-client-design.md`

## Global Constraints

- Preserve all existing `A2AAgent` and `A2ATCKAgent` behavior while moving their directories.
- Global `AgentApplication.UserAuthorization.AutoSignin` remains `false`.
- Never log, print, serialize, or persist access-token values.
- The console client sends tokens only in the HTTP `Authorization` header.
- The client obtains Agent API tokens, never Microsoft Graph tokens.
- `-delegated` and `-app` do not perform OBO.
- `-me` performs OBO to Microsoft Graph `User.Read`.
- Existing anonymous echo, multi-turn, direct-message, and streaming routes remain anonymous.
- Do not implement `TASK_STATE_AUTH_REQUIRED`, OAuth callbacks, continuation replay, or general A2A `UserState` identity changes.
- Use Central Package Management; do not add package versions to project files.
- Do not add `Task.Delay` to tests.

---

### Task 1: Move the Existing A2A Samples

**Files:**
- Move: `src/samples/A2AAgent/` to `src/samples/A2A/A2AAgent/`
- Move: `src/samples/A2ATCKAgent/` to `src/samples/A2A/A2ATCKAgent/`
- Create: `src/samples/A2A/README.md`
- Modify: `src/samples/A2A/A2AAgent/A2AAgent.csproj`
- Modify: `src/samples/A2A/A2ATCKAgent/A2ATCKAgent.csproj`
- Modify: `src/Microsoft.Agents.SDK.sln`
- Modify: any tracked file found by the old-path search below

**Interfaces:**
- Produces: stable project paths `samples\A2A\A2AAgent\A2AAgent.csproj` and `samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj`.
- Preserves: assembly names, namespaces, launch profiles, endpoint paths, and runtime behavior.

- [ ] **Step 1: Record all tracked references to the old paths**

Run:

```powershell
git grep -n -E "samples[\\/](A2AAgent|A2ATCKAgent)|A2AAgent[\\/]A2AAgent.csproj|A2ATCKAgent[\\/]A2ATCKAgent.csproj"
```

Expected: at minimum, both project entries in `src/Microsoft.Agents.SDK.sln`.

- [ ] **Step 2: Move both directories with Git**

Run:

```powershell
New-Item -ItemType Directory -Force src\samples\A2A | Out-Null
git mv src\samples\A2AAgent src\samples\A2A\A2AAgent
git mv src\samples\A2ATCKAgent src\samples\A2A\A2ATCKAgent
```

- [ ] **Step 3: Update project references for the added directory depth**

In both moved project files, change library references from:

```xml
<ProjectReference Include="..\..\libraries\..." />
```

to:

```xml
<ProjectReference Include="..\..\..\libraries\..." />
```

Do not add an analyzer reference already supplied by `src/samples/Directory.Build.props`.

- [ ] **Step 4: Update the solution paths without changing project GUIDs**

Change only the paths:

```text
samples\A2A\A2AAgent\A2AAgent.csproj
samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj
```

- [ ] **Step 5: Add the parent sample README**

Create `src/samples/A2A/README.md` with:

```markdown
# A2A samples

- [A2AAgent](A2AAgent/README.md) demonstrates A2A hosting, task lifecycle,
  streaming, multi-turn messages, and route-scoped OAuth.
- [A2ATCKAgent](A2ATCKAgent/README.md) hosts the endpoint used for A2A TCK
  compatibility testing.
- [A2AClient](A2AClient/README.md) is an interactive console client for
  anonymous, delegated, OBO, and application-token testing.
```

- [ ] **Step 6: Update every remaining tracked old-path reference**

Run the Step 1 search again. Update each result to the new `src/samples/A2A/...`
location. Do not modify generated `bin` or `obj` files.

- [ ] **Step 7: Build both moved projects**

Run:

```powershell
dotnet build src\samples\A2A\A2AAgent\A2AAgent.csproj --no-restore
dotnet build src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj --no-restore
```

Expected: both builds succeed with zero warnings and zero errors.

- [ ] **Step 8: Commit the relocation**

```powershell
git add src\samples\A2A src\Microsoft.Agents.SDK.sln
git commit -m "refactor: group A2A samples"
```

---

### Task 2: Create the Client Authentication Core

**Files:**
- Create: `src/samples/A2A/A2AClient/A2AClient.csproj`
- Create: `src/samples/A2A/A2AClient/A2AAuthMode.cs`
- Create: `src/samples/A2A/A2AClient/A2AClientAuthenticationOptions.cs`
- Create: `src/samples/A2A/A2AClient/IA2AAccessTokenProvider.cs`
- Create: `src/samples/A2A/A2AClient/IMsalTokenClient.cs`
- Create: `src/samples/A2A/A2AClient/MsalTokenClient.cs`
- Create: `src/samples/A2A/A2AClient/A2AAccessTokenProvider.cs`
- Create: `src/samples/A2A/A2AClient/A2AAuthenticationSession.cs`
- Create: `src/samples/A2A/A2AClient/AuthenticatedA2AHttpHandler.cs`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/Microsoft.Agents.Samples.A2A.Tests.csproj`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAccessTokenProviderTests.cs`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/AuthenticatedA2AHttpHandlerTests.cs`
- Modify: `src/Microsoft.Agents.SDK.sln`

**Interfaces:**
- Produces: `enum A2AAuthMode { None, Delegated, App }`.
- Produces: `Task<string?> IA2AAccessTokenProvider.GetAccessTokenAsync(A2AAuthMode mode, CancellationToken cancellationToken)`.
- Produces: `A2AAuthenticationSession.Mode` and `SetMode(A2AAuthMode mode)`.
- Produces: `AuthenticatedA2AHttpHandler`, which adds a bearer header only when a token is returned.

- [ ] **Step 1: Add failing token-provider tests**

Create tests with a fake `IMsalTokenClient`:

```csharp
[Fact]
public async Task GetAccessTokenAsync_None_ReturnsNull()
{
    var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
    var provider = new A2AAccessTokenProvider(msal.Object);

    var token = await provider.GetAccessTokenAsync(A2AAuthMode.None, CancellationToken.None);

    Assert.Null(token);
}

[Theory]
[InlineData(A2AAuthMode.Delegated, "delegated-token")]
[InlineData(A2AAuthMode.App, "app-token")]
public async Task GetAccessTokenAsync_UsesSelectedFlow(A2AAuthMode mode, string expected)
{
    var msal = new Mock<IMsalTokenClient>();
    msal.Setup(client => client.AcquireDelegatedTokenAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("delegated-token");
    msal.Setup(client => client.AcquireApplicationTokenAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("app-token");
    var provider = new A2AAccessTokenProvider(msal.Object);

    var token = await provider.GetAccessTokenAsync(mode, CancellationToken.None);

    Assert.Equal(expected, token);
}
```

- [ ] **Step 2: Add failing HTTP-handler tests**

Use a recording inner handler:

```csharp
[Fact]
public async Task SendAsync_AuthenticatedMode_AddsBearerHeader()
{
    var session = new A2AAuthenticationSession { Mode = A2AAuthMode.Delegated };
    var tokens = new Mock<IA2AAccessTokenProvider>();
    tokens.Setup(provider => provider.GetAccessTokenAsync(A2AAuthMode.Delegated, It.IsAny<CancellationToken>()))
        .ReturnsAsync("test-token");
    var recorder = new RecordingHttpMessageHandler();
    using var client = new HttpClient(new AuthenticatedA2AHttpHandler(session, tokens.Object)
    {
        InnerHandler = recorder
    });

    await client.GetAsync("https://agent.example/.well-known/agent-card.json");

    Assert.Equal("Bearer", recorder.Request!.Headers.Authorization!.Scheme);
    Assert.Equal("test-token", recorder.Request.Headers.Authorization.Parameter);
}
```

Add a second test for `A2AAuthMode.None` asserting
`recorder.Request.Headers.Authorization` is `null`.

- [ ] **Step 3: Run the new tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --no-restore
```

Expected: compilation fails because the client authentication types do not
exist.

- [ ] **Step 4: Create the console project**

Use this client project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IncludeAspNetSampleHelpers>false</IncludeAspNetSampleHelpers>
    <RootNamespace>Microsoft.Agents.Samples.A2AClient</RootNamespace>
    <AssemblyName>A2AClient</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="A2A" />
    <PackageReference Include="Microsoft.Identity.Client" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" />
    <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Microsoft.Agents.Samples.A2A.Tests" />
  </ItemGroup>
</Project>
```

Parse the small startup option set directly from `args`; do not add a
`System.CommandLine` dependency.

Use this test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\samples\A2A\A2AClient\A2AClient.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Implement options and mode types**

```csharp
internal enum A2AAuthMode
{
    None,
    Delegated,
    App
}

internal sealed class A2AClientAuthenticationOptions
{
    public required string TenantId { get; init; }
    public required string PublicClientId { get; init; }
    public required string ConfidentialClientId { get; init; }
    public required string ConfidentialClientSecret { get; init; }
    public required string AgentDelegatedScope { get; init; }
    public required string AgentApplicationScope { get; init; }
}
```

Validate only the fields required by the selected mode. `none` requires no
authentication values.

- [ ] **Step 6: Implement the MSAL boundary**

Define:

```csharp
internal interface IMsalTokenClient
{
    Task<string> AcquireDelegatedTokenAsync(CancellationToken cancellationToken);
    Task<string> AcquireApplicationTokenAsync(CancellationToken cancellationToken);
}
```

`MsalTokenClient` builds:

```csharp
PublicClientApplicationBuilder.Create(options.PublicClientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, options.TenantId)
    .WithDefaultRedirectUri()
    .Build();

ConfidentialClientApplicationBuilder.Create(options.ConfidentialClientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, options.TenantId)
    .WithClientSecret(options.ConfidentialClientSecret)
    .Build();
```

For delegated acquisition:

1. call `GetAccountsAsync()`;
2. if an account exists, try
   `AcquireTokenSilent([options.AgentDelegatedScope], account)`;
3. catch only `MsalUiRequiredException`;
4. call `AcquireTokenWithDeviceCode([options.AgentDelegatedScope], callback)`;
5. write `callback.Message` to the console, never token values;
6. return `AuthenticationResult.AccessToken`.

For application acquisition, call:

```csharp
AcquireTokenForClient([options.AgentApplicationScope])
    .ExecuteAsync(cancellationToken)
```

- [ ] **Step 7: Implement provider, session, and HTTP handler**

```csharp
internal sealed class A2AAccessTokenProvider(IMsalTokenClient msal) : IA2AAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync(A2AAuthMode mode, CancellationToken cancellationToken)
    {
        return mode switch
        {
            A2AAuthMode.None => null,
            A2AAuthMode.Delegated => await msal.AcquireDelegatedTokenAsync(cancellationToken).ConfigureAwait(false),
            A2AAuthMode.App => await msal.AcquireApplicationTokenAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
```

`AuthenticatedA2AHttpHandler.SendAsync` removes any pre-existing
`Authorization` header, requests the token for `session.Mode`, sets
`new AuthenticationHeaderValue("Bearer", token)` only for a non-empty token,
then sends the request.

- [ ] **Step 8: Run the focused tests and verify GREEN**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --no-restore
```

Expected: all authentication-core tests pass.

- [ ] **Step 9: Add both new projects to the solution and commit**

Run:

```powershell
dotnet sln src\Microsoft.Agents.SDK.sln add src\samples\A2A\A2AClient\A2AClient.csproj
dotnet sln src\Microsoft.Agents.SDK.sln add src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj
git add Directory.Packages.props src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests src\Microsoft.Agents.SDK.sln
git commit -m "feat: add A2A client authentication modes"
```

Omit `Directory.Packages.props` from `git add` when no package entry changed.

---

### Task 3: Implement the Interactive A2A Client

**Files:**
- Create: `src/samples/A2A/A2AClient/A2AClientOptions.cs`
- Create: `src/samples/A2A/A2AClient/A2AConsole.cs`
- Create: `src/samples/A2A/A2AClient/A2AResponseWriter.cs`
- Create: `src/samples/A2A/A2AClient/Program.cs`
- Create: `src/samples/A2A/A2AClient/appsettings.json`
- Create: `src/samples/A2A/A2AClient/Properties/launchSettings.json`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AConsoleTests.cs`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AResponseWriterTests.cs`
- Modify: `src/samples/A2A/A2AClient/A2AClient.csproj`

**Interfaces:**
- Consumes: `A2AAuthenticationSession`, `AuthenticatedA2AHttpHandler`, `A2ACardResolver`, `A2AClient`, and `IA2AClient`.
- Produces: `Task<int> A2AConsole.RunAsync(CancellationToken cancellationToken)`.
- Produces: interactive commands `:auth none`, `:auth delegated`, `:auth app`, `:history on|off`, `:q`, and `quit`.
- Produces: startup options `--agent`, `--history`,
  `--use-push-notifications`, and `--push-notification-receiver`.

- [ ] **Step 1: Write failing command tests**

Test a line-input abstraction rather than `Console.In` directly:

```csharp
[Theory]
[InlineData(":auth none", A2AAuthMode.None)]
[InlineData(":auth delegated", A2AAuthMode.Delegated)]
[InlineData(":auth app", A2AAuthMode.App)]
public void TryHandleCommand_AuthCommand_ChangesMode(string command, A2AAuthMode expected)
{
    var session = new A2AAuthenticationSession();
    var console = CreateConsole(session);

    var handled = console.TryHandleCommand(command);

    Assert.True(handled);
    Assert.Equal(expected, session.Mode);
}
```

Add tests that `:q` stops the loop and that ordinary text is not treated as a
command.

- [ ] **Step 2: Write failing response-formatting tests**

Build an `AgentTask` containing status text and artifact text. Assert
`A2AResponseWriter` returns both texts but does not include request headers or
any supplied sentinel token string.

- [ ] **Step 3: Run the client tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AConsoleTests|FullyQualifiedName~A2AResponseWriterTests" --no-restore
```

Expected: compilation fails because the console and writer do not exist.

- [ ] **Step 4: Implement configuration loading**

Use `ConfigurationBuilder` with:

```csharp
.SetBasePath(AppContext.BaseDirectory)
.AddJsonFile("appsettings.json", optional: false)
.AddEnvironmentVariables("A2ACLIENT_")
.AddUserSecrets<Program>(optional: true)
```

Add the required configuration packages through Central Package Management only
if they are not already available through the project SDK.

`A2AClientOptions` contains `Uri AgentUrl` and the authentication options from
Task 2. Fail startup with `InvalidOperationException` naming the missing key.

- [ ] **Step 5: Implement Agent Card resolution and client creation**

Create one `HttpClient` using `AuthenticatedA2AHttpHandler`. Resolve the card:

```csharp
var resolver = new A2ACardResolver(options.AgentUrl, httpClient);
var card = await resolver.GetAgentCardAsync(cancellationToken);
```

Select the first supported interface whose binding is JSON-RPC or HTTP+JSON.
Create the protocol client directly:

```csharp
IA2AClient client = interfaceDefinition.ProtocolBinding switch
{
    ProtocolBindingNames.JsonRpc => new global::A2A.A2AClient(new Uri(interfaceDefinition.Url), httpClient),
    ProtocolBindingNames.HttpJson => new A2AHttpJsonClient(new Uri(interfaceDefinition.Url), httpClient),
    _ => throw new InvalidOperationException(...)
};
```

Use `global::A2A` qualification to avoid conflict with the sample folder name.

- [ ] **Step 6: Implement the interactive loop**

Adapt the upstream A2ACli behavior:

1. display the Agent Card without request headers;
2. prompt for a command or message;
3. handle `:auth`, `:history`, and quit commands locally;
4. optionally prompt for one attachment path;
5. create `SendMessageRequest` with a generated message ID;
6. use streaming when the card advertises it and the operator enables it;
7. display status and artifact text through `A2AResponseWriter`;
8. when task state is `InputRequired`, reuse the task/context IDs for the next
   message;
9. fetch and display history when enabled;
10. when `--use-push-notifications` is set, include a
    `TaskPushNotificationConfig` using the URI from
    `--push-notification-receiver`, matching the upstream A2ACli payload.

Do not serialize the `HttpRequestMessage`, access-token provider, or
`AuthenticationResult`.

- [ ] **Step 7: Add committed non-secret configuration**

Use GUID-shaped example values:

```json
{
  "A2A": {
    "AgentUrl": "http://localhost:3978/a2a"
  },
  "Authentication": {
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "PublicClientId": "00000000-0000-0000-0000-000000000000",
    "ConfidentialClientId": "00000000-0000-0000-0000-000000000000",
    "ConfidentialClientSecret": "",
    "AgentDelegatedScope": "api://00000000-0000-0000-0000-000000000000/access_as_user",
    "AgentApplicationScope": "api://00000000-0000-0000-0000-000000000000/.default"
  }
}
```

Add `<UserSecretsId>AgentsSdk-A2AClient</UserSecretsId>` to the client project.

- [ ] **Step 8: Run tests and build**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --no-restore
dotnet build src\samples\A2A\A2AClient\A2AClient.csproj --no-restore
```

Expected: tests and build pass with zero warnings and zero errors.

- [ ] **Step 9: Commit the interactive client**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests
git commit -m "feat: add interactive A2A console client"
```

---

### Task 4: Add Route-Scoped OAuth Scenarios to A2AAgent

**Files:**
- Create: `src/samples/A2A/A2AAgent/GraphProfile.cs`
- Create: `src/samples/A2A/A2AAgent/IGraphProfileClient.cs`
- Create: `src/samples/A2A/A2AAgent/GraphProfileClient.cs`
- Create: `src/samples/A2A/A2AAgent/A2ATokenIdentity.cs`
- Modify: `src/samples/A2A/A2AAgent/MyAgent.cs`
- Modify: `src/samples/A2A/A2AAgent/Program.cs`
- Modify: `src/samples/A2A/A2AAgent/appsettings.json`
- Modify: `src/samples/A2A/A2AAgent/A2AAgent.csproj`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAgentOAuthRouteTests.cs`
- Modify: `src/tests/Microsoft.Agents.Samples.A2A.Tests/Microsoft.Agents.Samples.A2A.Tests.csproj`

**Interfaces:**
- Produces: `[A2AMessageRoute("-delegated", autoSignInHandlers: "delegated")]`.
- Produces: `[A2AMessageRoute("-me", autoSignInHandlers: "graph")]`.
- Produces: `[A2AMessageRoute("-app", autoSignInHandlers: "app")]`.
- Produces: `Task<GraphProfile> IGraphProfileClient.GetMeAsync(string accessToken, CancellationToken cancellationToken)`.

- [ ] **Step 1: Add the sample projects to the test project**

Add:

```xml
<ProjectReference Include="..\..\samples\A2A\A2AAgent\A2AAgent.csproj" />
<ProjectReference Include="..\..\samples\A2A\A2AClient\A2AClient.csproj" />
```

Add:

```xml
<InternalsVisibleTo Include="Microsoft.Agents.Samples.A2A.Tests" />
```

to `A2AAgent.csproj`.

- [ ] **Step 2: Write failing route registration tests**

Construct `MyAgent` with programmatic authorization handlers named
`delegated`, `graph`, and `app`, plus a mocked `IGraphProfileClient`.
Use the real A2A adapter and an authenticated request context to assert:

- `-delegated` invokes only the `delegated` handler;
- `-me` invokes only the `graph` handler;
- `-app` invokes only the `app` handler;
- an ordinary echo message invokes no authorization handler.

Use strict mocked `IUserAuthorization` instances that return distinct sentinel
tokens. Do not assert or print the sentinel token in response text.

Add wrong-mode assertions:

- an identity with `scp` is rejected by `-app`;
- an identity with `idtyp=app` and `roles` is rejected by `-delegated` and
  `-me`.

- [ ] **Step 3: Write failing Graph route test**

Configure the `graph` handler to return `"graph-token"` and:

```csharp
graphClient
    .Setup(client => client.GetMeAsync("graph-token", It.IsAny<CancellationToken>()))
    .ReturnsAsync(new GraphProfile("Ada Lovelace", "ada@example.com"));
```

Send `-me` through the A2A adapter and assert the completed task text contains
`Ada Lovelace` and `ada@example.com`.

- [ ] **Step 4: Run route tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AAgentOAuthRouteTests" --no-restore
```

Expected: compilation or assertion failures because the routes and Graph client
do not exist.

- [ ] **Step 5: Add the three authorization handlers to configuration**

Under `AgentApplication`:

```json
"UserAuthorization": {
  "DefaultHandlerName": "delegated",
  "AutoSignin": false,
  "Handlers": {
    "delegated": {
      "Type": "A2AUserAuthorization",
      "Settings": {}
    },
    "graph": {
      "Type": "A2AUserAuthorization",
      "Settings": {
        "OBOConnectionName": "ServiceConnection",
        "OBOScopes": [
          "User.Read"
        ]
      }
    },
    "app": {
      "Type": "A2AUserAuthorization",
      "Settings": {}
    }
  }
}
```

Keep `AutoSignin` false. Preserve the existing token-validation and connection
configuration.

- [ ] **Step 6: Implement the Graph client**

Register:

```csharp
builder.Services.AddHttpClient<IGraphProfileClient, GraphProfileClient>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
});
```

`GetMeAsync` sends `GET me?$select=displayName,userPrincipalName`, sets the
bearer header, and calls `EnsureSuccessStatusCode()`. Deserialize with
`System.Text.Json` into:

```csharp
internal sealed record GraphProfile(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("userPrincipalName")] string UserPrincipalName);
```

Do not catch `HttpRequestException` in the client. Let the route-level failure
path surface it through existing agent error handling.

- [ ] **Step 7: Implement protected routes**

Change the agent constructor to accept `IGraphProfileClient`.

Implement `A2ATokenIdentity` with:

```csharp
internal static void RequireDelegated(ClaimsIdentity identity)
{
    if (!identity.HasClaim(claim => claim.Type == "scp") ||
        identity.HasClaim("idtyp", "app"))
    {
        throw new InvalidOperationException("This route requires a delegated user token.");
    }
}

internal static void RequireApplication(ClaimsIdentity identity)
{
    var isApplication = identity.HasClaim("idtyp", "app") ||
        identity.HasClaim(claim => claim.Type == "roles");
    if (!isApplication || identity.HasClaim(claim => claim.Type == "scp"))
    {
        throw new InvalidOperationException("This route requires an application token.");
    }
}
```

The application identity must have `idtyp=app` or an application `roles` claim,
and it must not have `scp`.

For delegated and app routes:

```csharp
var _ = await UserAuthorization.GetTurnTokenAsync(turnContext, "delegated", cancellationToken);
var identity = turnContext.Identity;
A2ATokenIdentity.RequireDelegated(identity);
var summary = BuildIdentitySummary(identity, includeApplicationId: false);
await CompleteTaskAsync(turnContext, summary, cancellationToken);
```

Use `"app"`, `A2ATokenIdentity.RequireApplication(identity)`, and
`includeApplicationId: true` for the application route.
`BuildIdentitySummary` includes only claim values for:

- tenant: `tid`;
- object ID: `oid`;
- subject: `sub`;
- application ID: `azp` or `appid` for app mode;
- authentication type.

It must not include the bearer token, all claims indiscriminately, or
`ITurnState.User`.

For `-me`:

```csharp
var token = await UserAuthorization.GetTurnTokenAsync(turnContext, "graph", cancellationToken);
A2ATokenIdentity.RequireDelegated(turnContext.Identity);
var profile = await graphClient.GetMeAsync(token, cancellationToken);
await CompleteTaskAsync(
    turnContext,
    $"Name: {profile.DisplayName}{Environment.NewLine}User principal name: {profile.UserPrincipalName}",
    cancellationToken);
```

`CompleteTaskAsync` sends one `EndOfConversation` activity with
`EndOfConversationCodes.CompletedSuccessfully`.

- [ ] **Step 8: Run route tests and verify GREEN**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AAgentOAuthRouteTests" --no-restore
```

Expected: all protected and anonymous route tests pass.

- [ ] **Step 9: Build the agent and commit**

Run:

```powershell
dotnet build src\samples\A2A\A2AAgent\A2AAgent.csproj --no-restore
git add src\samples\A2A\A2AAgent src\tests\Microsoft.Agents.Samples.A2A.Tests
git commit -m "feat: demonstrate route-scoped A2A OAuth"
```

---

### Task 5: Document Entra Setup and Manual Scenarios

**Files:**
- Create: `src/samples/A2A/A2AClient/README.md`
- Modify: `src/samples/A2A/A2AAgent/README.md`
- Modify: `src/samples/A2A/README.md`

**Interfaces:**
- Produces: reproducible setup for anonymous, delegated passthrough, delegated
  OBO, and application-token manual testing.

- [ ] **Step 1: Document the Agent API registration**

Add exact portal steps:

1. create a single-tenant Agent API app registration;
2. expose `api://<agent-client-id>/access_as_user`;
3. add an app role named `A2A.Access` with allowed member type
   `Applications`;
4. add delegated Microsoft Graph `User.Read`;
5. grant consent required by the tenant;
6. create the client secret or credential used by `ServiceConnection`;
7. configure `TokenValidation.Audiences` with the Agent API client ID.

- [ ] **Step 2: Document the public client**

Include:

1. create a public client registration;
2. enable public client flows;
3. add delegated permission to the Agent API `access_as_user` scope;
4. set `Authentication:PublicClientId`;
5. run `:auth delegated`;
6. send `-delegated`;
7. send `-me`.

- [ ] **Step 3: Document the confidential client**

Include:

1. create a confidential client registration;
2. add the Agent API `A2A.Access` application permission;
3. grant admin consent;
4. create a client secret;
5. store it with:

```powershell
dotnet user-secrets --project src\samples\A2A\A2AClient\A2AClient.csproj set "Authentication:ConfidentialClientSecret" "<secret>"
```

6. set `ConfidentialClientId`;
7. run `:auth app`;
8. send `-app`.

Explicitly warn not to commit the secret.

- [ ] **Step 4: Document expected failures**

State:

- `-me` with `:auth none` fails because the route requires a validated token;
- `-me` with `:auth app` fails because OBO requires a user-delegated token;
- `-app` with `:auth delegated` may authenticate but represents the wrong
  scenario and must not be interpreted as application identity;
- a Graph token must not be pasted or sent directly to the Agent API.

- [ ] **Step 5: Verify every documented command and path**

Run:

```powershell
Test-Path src\samples\A2A\A2AAgent\A2AAgent.csproj
Test-Path src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj
Test-Path src\samples\A2A\A2AClient\A2AClient.csproj
git grep -n "src/samples/A2AAgent\|src\\samples\\A2AAgent\|src/samples/A2ATCKAgent\|src\\samples\\A2ATCKAgent"
```

Expected: all `Test-Path` calls return `True`; the old-path search returns no
matches.

- [ ] **Step 6: Commit the documentation**

```powershell
git add src\samples\A2A\README.md src\samples\A2A\A2AAgent\README.md src\samples\A2A\A2AClient\README.md
git commit -m "docs: explain A2A OAuth sample setup"
```

---

### Task 6: Validate the Complete A2A Sample Set

**Files:**
- Modify only files required by failures directly caused by Tasks 1-5.

**Interfaces:**
- Produces: buildable, test-covered A2A samples and manual OAuth workflows.

- [ ] **Step 1: Run the new sample tests**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --no-restore
```

- [ ] **Step 2: Run existing A2A extension tests**

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --no-restore
```

- [ ] **Step 3: Run targeted Builder authorization tests**

```powershell
dotnet test src\tests\Microsoft.Agents.Builder.Tests\Microsoft.Agents.Builder.Tests.csproj --filter "FullyQualifiedName~UserAuthenticationFeatureTests|FullyQualifiedName~AzureBotUserAuthorizationTests|FullyQualifiedName~UserAuthorizationModuleLoaderTests" --no-restore
```

- [ ] **Step 4: Build all A2A sample projects**

```powershell
dotnet build src\samples\A2A\A2AAgent\A2AAgent.csproj --no-restore
dotnet build src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj --no-restore
dotnet build src\samples\A2A\A2AClient\A2AClient.csproj --no-restore
```

- [ ] **Step 5: Build the solution**

```powershell
dotnet build src\Microsoft.Agents.SDK.sln --no-restore
```

- [ ] **Step 6: Review the final diff**

Confirm:

- the two existing projects are moves, not delete-and-recreate copies;
- no generated `bin` or `obj` files are tracked;
- no token or client secret value is committed;
- global auto-sign-in is false;
- only `-delegated`, `-me`, and `-app` declare authorization handlers;
- the delegated and application handlers do not configure OBO;
- the Graph handler configures `User.Read`;
- the client reuses an authenticated `HttpClient` for card and task requests;
- old sample paths no longer appear in tracked files;
- no callback, `AUTH_REQUIRED`, or UserState production code was added.

- [ ] **Step 7: Run the repository diff checks**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and only intended source, project, solution, and
documentation changes. If validation requires a correction, return to the task
that owns the affected file, make the correction, rerun that task's focused
test, and amend the task only when the user has explicitly authorized amending;
without that authorization, create a new fix commit whose message names the
corrected behavior.
