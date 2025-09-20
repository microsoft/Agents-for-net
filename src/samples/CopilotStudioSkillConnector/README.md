# CopilotStudioSkillNext

- The sample uses the Agent SDK User Authorization capabilities the Microsoft Copilot Studio Skill Connector.
- This sample shows how to use an OBO Exchange to communicate back to Microsoft Copilot Studio using the CopilotStudioClient.

- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)


## Running this sample

1. Create a Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
   1. Publish your newly created Copilot
   1. Go to Settings => Advanced => Metadata and copy the following values. You will need them later:
      1. Schema name
      1. Environment Id
       
2. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md)
   - [SingleTenant, Federated Credentials](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-fic.md) 

3. Configuring the authentication connection in the Agent settings
   > These instructions are for **SingleTenant, Client Secret**. For other auth type configuration, see [DotNet MSAL Authentication](https://github.com/microsoft/Agents/blob/main/docs/HowTo/MSALAuthConfigurationOptions.md).
   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections` and `TokenValidation`,  it should appear similar to this:

      ```json
      "TokenValidation": {
        "Enabled": true,
        "Audiences": [
            "{{ClientId}}"
        ],
        "TenantId": "{{TenantId}}"
      },

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

   1. Setup the Copilot Studio Agent information
      ```json
      "CopilotStudioAgent": {
        "EnvironmentId": "", // Environment ID of environment with the CopilotStudio App.
        "SchemaName": "", // Schema Name of the Copilot to use
      }
      ```

4. Add the MCS Connector Agent
   - TBD
 
## Further reading
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
