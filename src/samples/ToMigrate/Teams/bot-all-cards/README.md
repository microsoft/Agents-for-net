# Types of Cards

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents).
This sample shows the feature where user can send different types of cards using bot.

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

1. Run the `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).
 
   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

- **This step is specific to Teams.**

1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     -  Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Start the Agent, and select **Preview in Teams** in the upper right corner


## Interacting with this Agent in Teams

**Install App:**

![Installapp](Images/1.Install.png)

**Welcome Cards:**

![WelcomeCards](Images/2.Welcome.png)

**All Cards:**

![AllCards](Images/3.SelectCards.png)

**Adaptive Card:**

![AdaptiveCard](Images/4.AdaptiveCard.png)

Add media url from sharepoint or onedrive to the text input to get media loaded to the adaptive card. For more information refer [media elements in card.](https://review.learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/media-elements-in-adaptive-cards?branch=pr-en-us-8333&tabs=desktop) 

![AdaptiveCardMedia](Images/AdaptiveCardMedia.png)

![AdaptiveCardMedia2](Images/AdaptiveCardMedia2.png)

**Hero Card:**

![HeroCard](Images/5.HeroCard.png)

**OAuth Card:**

![OAuthCard](Images/6.OathCard.png)

**Signin Card:**

![SigninCard](Images/7.SignInCard.png)

**Thumbnail Card:**

![ThumbnailCard](Images/8.ThumbnailCard.png)

**List Card:**

![ListCards](Images/9.ListCard.png)

**Collections Card:**

![CollectionsCards](Images/10.CollectionCard.png)

**Connector Card:**

![ConnectorCards](Images/11.ConnectorCard.png)

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.