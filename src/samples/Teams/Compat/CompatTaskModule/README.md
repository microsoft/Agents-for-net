# Teams Dialog (referred as task modules in TeamsJS v1.x)

This sample has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents). It shows how to fetch a Dialog (referred as task modules in TeamsJS v1.x) from Hero Card or Adaptive Card buttons and receive input from the Dialog (referred as task modules in TeamsJS v1.x) in the bot.

## Included Features
* Agent
* Tabs
* Dialogs (referred as task modules in TeamsJS v1.x)

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

## Interacting with this Agent in Teams

You can interact with this bot by sending it a message. The bot will respond with a Hero Card and Adaptive Card with buttons which will display a Dialog (referred as task modules in TeamsJS v1.x) when clicked. The Dialogs (referred as task modules in TeamsJS v1.x) demonstrate retrieving input from a user, or displaying custom web page content.

- **Personal Scope Interactions:**

![1.InstallApp ](Images/1.InstallApp.png)

![2.Dialogs ](Images/2.Dialogs.png)

![2.GroupDialogs ](Images/2.GroupDialogs.png)

![3.AdaptiveCard ](Images/3.AdaptiveCard.png)

![4.ThanksAdaptiveCard ](Images/4.ThanksAdaptiveCard.png)

![5.CustomForm ](Images/5.CustomForm.png)

![6.Results ](Images/6.Results.png)

![7.YouTube ](Images/7.YouTube.png)

![8.Task ](Images/8.Task.png)

![9.PowerApp ](Images/9.PowerApp.png)

![10.GroupChat ](Images/10.GroupChat.png)

![11.GroupCustomForm ](Images/12.GroupCustomForm.png)

![12.GroupResults ](Images/13.GroupResults.png)

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.