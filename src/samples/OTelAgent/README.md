# OTelAgent Sample (OpenTelemetry + Microsoft 365 Agents SDK)

This sample shows a simple Agent hosted as an ASP.NET Core web app instrumented end‑to‑end with OpenTelemetry (traces, metrics, and logs) and optionally exporting to Azure Monitor / Application Insights.  
It echoes user messages and demonstrates how to add custom Activities, counters, histograms, and enrichment for inbound and outbound HTTP operations.

The sample helps you:
- Understand the Microsoft 365 Agents SDK messaging loop.
- Learn how to integrate OpenTelemetry in an Agent (configuration, custom telemetry, enrichment).
- Use conditional HttpClient instrumentation and Azure Monitor export.

## Key OpenTelemetry Features in This Sample

| Area | What the sample demonstrates |
|------|------------------------------|
| Resource | `AddService()` with `service.name=OTelAgent`, `service.version=1.0.0`, environment + namespace attributes. |
| Tracing | ASP.NET Core + HttpClient instrumentation (toggle), custom `ActivitySource` (`OTelAgent`, `OTelAgent.MyAgent`), manual spans for message processing and route handlers. |
| Metrics | Custom `Counter`, `Histogram`, `UpDownCounter` for agent message + route telemetry; optional HttpClient metrics; custom histogram view for `http.client.duration` (if present in your dependencies). |
| Logging | OpenTelemetry logging provider (structured logs flow to Azure Monitor when connection string is supplied). |
| Export | Conditional Azure Monitor exporter when `ApplicationInsights:ConnectionString` (or `APPLICATIONINSIGHTS_CONNECTION_STRING`) is set. |
| Enrichment | Adds request/response sizes, user agent, outbound HTTP tags, timing for external call, and exception tagging. |
| Filtering | Demonstrates how to suppress outbound HTTP telemetry (example health check filter placeholder). |
| Configuration Toggle | `OpenTelemetry:EnableHttpClient` enables/disables HttpClient instrumentation without recompiling. |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Dev Tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local Bot / external channel testing)
- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit) (optional rapid testing via Agents Playground)
- (Optional) Azure Application Insights resource if you want to export telemetry.


## QuickestStart using Agent Toolkit
1. If you haven't done so already, install the Agents Playground
 
   ```
   winget install agentsplayground
   ```
1. Start the Agent in VS or VS Code in debug
1. Start Agents Playground.  At a command prompt: `agentsplayground`
   - The tool will open a web browser showing the Microsoft 365 Agents Playgroun, ready to send messages to your agent. 
1. Interact with the Agent via the browser

## QuickStart using WebChat or Teams

- Overview of running and testing an Agent
  - Provision an Azure Bot in your Azure Subscription
  - Configure your Agent settings to use to desired authentication type
  - Running an instance of the Agent app (either locally or deployed to Azure)
  - Test in a client

1. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md)
   - [SingleTenant, Federated Credentials](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-fic.md) 
   - [User Assigned Managed Identity](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-msi.md)

1. Configuring the authentication connection in the Agent settings
   > These instructions are for **SingleTenant, Client Secret**. For other auth type configuration, see [DotNet MSAL Authentication](https://github.com/microsoft/Agents/blob/main/docs/HowTo/MSALAuthConfigurationOptions.md).
   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{ClientId}}", // this is the Client ID used for the connection.
            "ClientSecret": "{{ClientSecret}}", // this is the Client Secret used for the connection.
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      },
      ```

      1. Replace all **{{ClientId}}** with the AppId of the Azure Bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **{{ClientSecret}}** to the Secret that was created on the App Registration.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.
   1. If using Azure Monitor, set `APPLICATIONINSIGHTS_CONNECTION_STRING` To expose your local Agent:

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

   1. Deploy Agent code to Azure
      1. VS Publish works well for this.  But any tools used to deploy a web application will also work.
      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `https://{{appServiceDomain}}/api/messages`

## Testing this agent with WebChat

   1. Select **Test in WebChat** on the Azure Bot

## Testing this Agent in Teams or M365

1. Update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
     - `manifest.json`
     - `outline.png`
     - `color.png`

1. Your Azure Bot should have the **Microsoft Teams** channel added under **Channels**.

1. Navigate to the Microsoft Admin Portal (MAC). Under **Settings** and **Integrated Apps,** select **Upload Custom App**.

1. Select the `manifest.zip` created in the previous step. 

1. After a short period of time, the agent shows up in Microsoft Teams and Microsoft 365 Copilot.


## OpenTelemetry Implementation Details

### Tracing
- Registered sources: `"OTelAgent"`, `"OTelAgent.MyAgent"`, SDK sources, `"System.Net.Http"`.
- Custom Activities:
  - `agent.process_message` (request pipeline)
  - `agent.message_handler`
  - `agent.welcome_message`
  - Outbound call events: `external_call.started`, `external_call.completed`
- Enrichment adds tags (conversation id, user id, request sizes, status codes).
- Exceptions captured with full stack.

### Metrics
Defined in `AgentTelemetry`:
- `agent.messages.processed` (Counter<long>)
- `agent.routes.executed` (Counter<long>)
- `agent.message.processing.duration` (Histogram<double>, ms)
- `agent.route.execution.duration` (Histogram<double>, ms)
- `agent.conversations.active` (UpDownCounter<long>)

HttpClient metrics included if enabled.

### Logs
`builder.Logging.AddOpenTelemetry()` adds structured log export (when Azure Monitor exporter active).

### Azure Monitor Export (Optional)
Conditional:
if (!string.IsNullOrWhiteSpace(aiConnectionString)) { otelBuilder.UseAzureMonitorExporter(o => o.ConnectionString = aiConnectionString); }


### HttpClient Instrumentation Toggle
`OpenTelemetry:EnableHttpClient` (default true). Disabling removes:
- Outbound HTTP automatic spans
- HttpClient metric collection (only manual spans remain)

### Manual Outbound Call Example (in `MyAgent.cs`)
Performs a GET to `https://www.bing.com` and records:
- External call duration
- HTTP status code
- Custom events for start/completion

### Custom Histogram View (Optional Pattern)
You can add custom bucket configuration (example pattern shown in comments for `http.client.duration` if supported in your dependencies).


## Enabling JWT token validation
1. By default, the AspNet token validation is disabled in order to support local debugging.
1. Enable by updating appsettings
   ```json
   "TokenValidation": {
     "Enabled": false,
     "Audiences": [
       "{{ClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Further Reading

- Microsoft 365 Agents SDK: https://github.com/microsoft/agents
- OpenTelemetry .NET Docs: https://opentelemetry.io/docs/instrumentation/net/
- Azure Monitor OpenTelemetry Exporter: https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable
- ASP.NET Core Configuration Best Practices: https://learn.microsoft.com/aspnet/core/fundamentals/configuration/
