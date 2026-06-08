# AgenticTeamsOAuth Sample

This sample demonstrates how to configure and run an Agentic Agent in Teams that can authenticate users via OAuth.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## QuickStart using Teams

- Overview of running and testing an Agent
  - The Agentic App has already been created.  You will need that ClientId, TenantId, and secret.
  - Configure your Agent settings to use the desired authentication type
  - Running an instance of the Agent app (either locally or deployed to Azure)

1. Configuring the authentication connection in the Agent settings
   > These instructions are for **SingleTenant, Client Secret**. For other auth type configuration, see [DotNet MSAL Authentication](https://github.com/microsoft/Agents/blob/main/docs/HowTo/MSALAuthConfigurationOptions.md).
   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
        "BlueprintConnection": {
          "Settings": {
            "AuthType": "ClientSecret",
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{BlueprintClientId}}",
            "ClientSecret": "{{BlueprintClientSecret}}",
            "Scopes": [
              "5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default"
            ]
          }
        }
      },
      ```

      1. Replace all **{{BlueprintClientId}}** with the AppId of the Azure Bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **{{BlueprintClientSecret}}** to the Secret that was created on the App Registration.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Create an additional App Registration to handle the OAuth exchange
   1. Set web redirect to `{{devtunnel_url}}/auth/callback`

    1. Find the section labeled `Connections`,  it should appear similar to this:

    ```json
    "Connections": {
    "UserOAuthConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
        "ClientId": "{{OAuthClientId}}",
        "ClientSecret": "{{OAuthClientSecret}}",
        "Scopes": [
            "5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default"
        ]
      }
    }
    ```

    1. Replace all **{{OAuthClientId}}** with the AppId of the Azure Bot.
    1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
    1. Set the **{{OAuthClientSecret}}** to the Secret that was created on the App Registration.

1. Running the Agent
   1. Running the Agent locally
      - Requires a tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.
      - **For ClientSecret or Certificate authentication types only.**  Federated Credentials and Managed Identity will not work via a tunnel to a local agent and must be deployed to an App Service or container.
      
      1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

         ```bash
         devtunnel host -p 3978 --allow-anonymous
         ```

      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

      1. Start the Agent in Visual Studio

## Enabling JWT token validation
1. By default, the AspNet token validation is disabled in order to support local debugging.
1. Enable by updating appsettings
   ```json
   "TokenValidation": {
     "Enabled": false,
     "Audiences": [
       "{{BlueprintClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

## Multi-Node Deployment: MSAL Distributed Token Cache

By default, `TeamsAgenticAuthorization` uses MSAL's in-memory token cache. This works for single-instance deployments — after the user signs in, subsequent turns return a cached token via `AcquireTokenSilent` without re-prompting.

**For multi-node clusters** (e.g., Azure App Service with multiple instances, Kubernetes), the in-memory cache is not shared across nodes. A user who signs in on Node A will be prompted again if their next request lands on Node B.

To solve this, configure a **distributed token cache** backed by Redis, SQL Server, or any `IDistributedCache` implementation.

### Setup with Redis

1. Add the NuGet package:

   ```bash
   dotnet add package Microsoft.Identity.Web.TokenCache
   ```

2. Register the distributed cache and attach it to MSAL in `Program.cs`:

   ```csharp
   using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

   // Add a distributed cache (Redis example)
   builder.Services.AddStackExchangeRedisCache(options =>
   {
       options.Configuration = builder.Configuration.GetConnectionString("Redis");
   });

   // Attach the distributed cache to MSAL's token cache
   builder.Services.AddDistributedTokenCaches();
   ```

3. Add the Redis connection string to `appsettings.json`:

   ```json
   "ConnectionStrings": {
     "Redis": "your-redis-host:6380,password=...,ssl=True,abortConnect=False"
   }
   ```

### Setup with SQL Server

1. Add the NuGet packages:

   ```bash
   dotnet add package Microsoft.Extensions.Caching.SqlServer
   dotnet add package Microsoft.Identity.Web.TokenCache
   ```

2. Create the cache table:

   ```bash
   dotnet sql-cache create "YourConnectionString" dbo TokenCache
   ```

3. Register in `Program.cs`:

   ```csharp
   using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

   builder.Services.AddDistributedSqlServerCache(options =>
   {
       options.ConnectionString = builder.Configuration.GetConnectionString("SqlCache");
       options.SchemaName = "dbo";
       options.TableName = "TokenCache";
   });

   builder.Services.AddDistributedTokenCaches();
   ```

### How It Works

- `Microsoft.Identity.Web.TokenCache` hooks into MSAL's `SetBeforeAccess` / `SetAfterAccess` events on the `IConfidentialClientApplication` token cache.
- When `AcquireTokenByAuthorizationCode` runs (in the OAuth callback), the tokens are serialized to the distributed store.
- When `AcquireTokenSilent` runs (on subsequent turns, potentially on a different node), the tokens are deserialized from the distributed store.
- Token cache entries are keyed by the user's home account ID, so each user's tokens are isolated.

### Notes

- The distributed cache is **optional** — single-instance deployments work without it.
- Ensure the cache has appropriate TTL settings. MSAL manages token expiry internally, but stale cache entries should be evicted.
- For production, use TLS-encrypted connections to Redis/SQL to protect tokens in transit.