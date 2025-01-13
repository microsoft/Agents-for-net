# Bot1 Sample

This is a sample of a simple Agent hosted on an Asp.net core web service, That represents the entry point of a multiBot configuration.  This Agent is configured to accept a request and echo the text of the request back to the caller or relay them to Bot2.

This Agent Sample is intended to introduce you the basic operation of the Microsoft 365 Agents SDK's messaging loop and how it can communicate with another Microsoft 365 Agent SDK bot.

## Prerequisites

**To run the sample on a development workstation (local development), the following tools and SDK's are required:**

- [.NET SDK](https://dotnet.microsoft.com/download) version 8.0
- Visual Studio 2022+ with the .net workload installed.
- [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases) for Testing Web Chat.

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure Entra ID Application Identities
    - Create and configure an [Azure Bot Service](https://aka.ms/AgentsSDK-CreateBot) for your bot
    - Create and configure an [Azure App Service](https://learn.microsoft.com/azure/app-service/) to deploy your bot on to.
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.

### QuickStart using WebChat

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:
      ```json
       "TokenValidation": {
         "Audiences": [
           "00000000-0000-0000-0000-000000000000" // this is the Client ID used for the Azure Bot
         ],
         "TenantId": "00000000-0000-0000-0000-000000000000"
       },

       "Connections": {
         "BotServiceConnection": {
           "Assembly": "Microsoft.Agents.Authentication.Msal",
           "Type":  "MsalAuth",
           "Settings": {
             "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
             "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
             "ClientId": "00000000-0000-0000-0000-000000000000", // this is the Client ID used for the connection.
             "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
             "Scopes": [
               "https://api.botframework.com/.default"
            ]
         }
       }
      ```
    
1. Set the **ClientId** to the AppId of the bot identity.
1. Set the **ClientSecret** to the Secret that was created for your identity.
1. Set the **TokenValidation:TenantId** to the Tenant Id where your application is registered.
1. Set the **TokenValidation:Audiences** to the AppId of the bot identity.

1. Find the section labeled `ChannelHost`, it should appear similar to this:

   ```json
   "ChannelHost": {
     "HostAppId": "00000000-0000-0000-0000-000000000000", // This is the Client ID used for the remote bot to call you back with.,
     "Channels": [
       {
         "Id": "EchoBot",
         "ChannelFactory": "HttpBotChannelFactory", // The name of the keyed IChannelFactory registered in DI
         "Settings": {
           "ClientId": "00000000-0000-0000-0000-000000000000", // This is the Client ID of the other agent.
           "TokenProvider": "BotServiceConnection", // Name of the connection to use to get the token from the connections array.
           "Endpoint": "http://localhost:39783/api/messages",
           "ServiceUrl": "http://localhost:3978/api/botresponse", // ServiceUrl for response (non-streamed responses)
           "ResourceUrl": null, // This is the Resource URL when creating the token, defaults to "api://{clientId}}
           "NamedClient": null // optional named HttpClient
         }
       }
     ]
   }
 
1. Set the **HostAppId** to the AppId of Bot1.
1. Set the **ClientId** to the AppId of the of Bot2.
1. Set the ChannelHost:Channels[Id="EchoBot"].Settings.Endpoint to the correct port used when running Bot2.
1. Set the ChannelHost:Channels[Id="EchoBot"].Settings.ServiceUrl to the Bot1 api/messages endpoint

> Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).    

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent in Visual Studio

1. Select **Test in WebChat** on the Azure Bot
