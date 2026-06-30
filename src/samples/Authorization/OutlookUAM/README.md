# Outlook Universal Action Message OAuth Sample

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents). It demonstrates how to use the [Universal Action Message](https://learn.microsoft.com/en-us/microsoft-365/outlook/actionable-messages/overview?view=o365-worldwide) (UAM) feature in Outlook with OAuth authentication.

## Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) (for Webpage Dialog, so Teams can reach your localhost)

## Running This Sample

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Add the Outlook Channel
   - Record the Application ID, Tenant ID, and Client Secret

1. Configure `appsettings.json`:

   ```json
   "Connections": {
     "ServiceConnection": {
       "Settings": {
         "AuthType": "ClientSecret",
         "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
         "ClientId": "{{ClientId}}",
         "ClientSecret": "{{ClientSecret}}",
         "Scopes": [
           "https://api.botframework.com/.default"
         ]       
       }
     }
   }
   ```

   Replace `{{ClientId}}`, `{{TenantId}}`, and `{{ClientSecret}}` with your Azure Bot values.

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

## Enabling JWT token validation
1. By default, the AspNet token validation is disabled in order to support local debugging.
1. Enable by updating appsettings
   ```json
   "TokenValidation": {
     "Audiences": [
       "{{ClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
