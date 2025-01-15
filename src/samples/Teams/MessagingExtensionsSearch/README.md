# Messaging Extensions Search

This sample has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents).

This sample illustrates how to create a [Search-based](https://docs.microsoft.com/microsoftteams/platform/messaging-extensions/how-to/search-commands/define-search-command) messaging extension in Microsoft Teams using the Microsoft 365 Agents SDK. It enables users to search for and select items via the command bar, supporting various interactions such as querying data and selecting results for further actions.

[Messaging Extensions](https://docs.microsoft.com/microsoftteams/platform/messaging-extensions/what-are-messaging-extensions) are a special kind of Microsoft Teams application that is support by the [Microsoft 365 Agents SDK](https://github.com/microsoft/agents).

## Included Features
* Agent
* Message Extensions
* Search Commands

![msgext-search ](Images/msgext-search.gif)

## Prerequisites

-  Microsoft Teams is installed and you have an account (not a guest account)
-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
-  [M365 developer account](https://docs.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/prepare-your-o365-tenant) or access to a Teams account with the appropriate permissions to install an app.

## Running this sample

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Be sure to add the Teams Channel
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "TokenValidation": {
        "Audiences": [
          "00000000-0000-0000-0000-000000000000" // this is the Client ID used for the Azure Bot
        ]
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
              ],
              "TenantId": "{{TenantId}}" // This is the Tenant ID used for the Connection. 
          }
      }
      ```

      1. Set the **ClientId** to the AppId of the bot identity.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      1. Set the **TenantId** to the Tenant Id where your application is registered.
      1. Set the **Audience** to the AppId of the bot identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Set `{{BaseUrl}}` in the `appsettings.json` as per your application using dev tunnels, your URL will be like: https://12345.devtunnels.ms.

1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     -  Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent, and select **Preview in Teams** in the upper right corner

**Note**: If you are facing any issue in your app, please uncomment [this](https://github.com/OfficeDev/Microsoft-Teams-Samples/blob/main/samples/msgext-search/csharp/AdapterWithErrorHandler.cs#L25) line and put your debugger for local debug.

## Interacting with this Agent in Teams

> Note this `manifest.json` specified that the feature will be available from both the `compose` and `commandBox` areas of Teams. Please refer to Teams documentation for more details.

In Teams, the command bar is located at the top of the window. When you at mention the bot what you type is forwarded (as you type) to the bot for processing. By way of illustration, this sample uses the text it receives to query the NuGet package store.

**Mention In Search CommandBar:**
  ![8-mention-Search-CommandBar ](Images/8-mention-Search-CommandBar.png)

**Search Result:**
  ![9-mention-Search-Result ](Images/9-mention-Search-Result.png)

**Selected Item:**
  ![10-mention-Search-SelectedItem ](Images/10-mention-Search-SelectedItem.png)

There is a secondary, drill down, event illustrated in this sample: clicking on the results from the initial query will result in the bot receiving another event.
![5-search-Result-ME ](Images/5-search-Result-ME.png)

![6-selected-Item-ME ](Images/6-selected-Item-ME.png)

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.