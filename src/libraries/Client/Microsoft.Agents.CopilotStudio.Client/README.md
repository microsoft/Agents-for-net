# Microsoft.Agents.CopilotStudio.Client

Provides a client to interact with agents built in Copilot Studio. This Library is intended to provide access to a given agent's conversational channel.

## Instructions - Required Setup to use this library

### Prerequisite

To use this library, you will need the following:

1. An Agent Created in Microsoft Copilot Studio, or access to an existing Agent.
1. Ability to Create a Application Identity in Azure for a Public Client/Native App Registration Or access to an existing Public Client/Native App registration with the CopilotStudio.Copilot.Invoke API Permission assigned.

### Create a Agent in Copilot Studio

1. Create a Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
    1. Publish your newly created Copilot
    1. Goto Settings => Advanced => Metadata and copy the following values, You will need them later:
        1. Schema name - this is the 'unique name' of your agent inside this environment.
        1. Environment Id - this is the ID of the environment that contains the agent.

### Create an Application Registration in Entra ID to support user authentication to Copilot Studio

This step will require permissions to Create application identities in your Azure tenant. For user authentication, you will be creating a Native Client Application Identity, which does not have secrets.

1. Open <https://portal.azure.com>
1. Navigate to Entra Id
1. Create an new App Registration in Entra ID
    1. Provide an Name
    1. Choose "Accounts in this organization directory only"
    1. In the "Select a Platform" list, Choose "Public Client/native (mobile & desktop)
    1. In the Redirect URI url box, type in `http://localhost` (**note: use HTTP, not HTTPS**)
    1. Then click register.
1. In your newly created application
    1. On the Overview page, Note down for use later when configuring the example application:
        1. the Application (client) ID
        1. the Directory (tenant) ID
    1. Goto Manage
    1. Goto API Permissions
    1. Click Add Permission
        1. In the side pannel that appears, Click the tab `API's my organization uses`
        1. Search for `Power Platform API`.
            1. *If you do not see `Power Platform API` see the note at the bottom of this section.*
        1. In the permissions list choose `CopilotStudio` and Check `CopilotStudio.Copilots.Invoke`
        1. Click `Add Permissions`
    1. (Optional) Click `Grant Admin consent for copilotsdk`
    1. Close Azure Portal

> [!TIP]
> If you do not see `Power Platform API` in the list of API's your organization uses, you need to add the Power Platform API to your tenant. To do that, goto [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) and follow the instructions on Step 2 to add the Power Platform Admin API to your Tenant

## How-to use

### User Based auth flows

User based authentication flows are the only currently supported flow for this client.

Your code will need to create a User Token ( via MSAL Public Client or an OBO flow for a User access token) to call this service.

There are currently two ways to pass auth to the CopilotClient.

#### 1. Create an HttpMessageRequest Handler that will populate the bearer token and assign it to the httpClient you create

In this case, you would create an HttpMessageRequestHandler that creates the bearer header on the http request message,  In the example below, AddTokenHandler is responsible for adding the bearer header to the http request prior to the send:

```cs
// Create an http client for use by the DirectToEngine Client and add the token handler to the client.
builder.Services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(() => new AddTokenHandler(settings));
```

Then instance the client as such:

```cs
var copilotClient = new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent:true, cancellationToken:cancellationToken))
{

}
```

#### 2. Create a function that returns a user token and pass that to the constructor for the Copilot Client

In this case, the TokenService is a class is responsible for managing the token acquire flow and returning the access token via the GetToken API call on the TokenService Class.

```cs
var tokenService = new TokenService(settings);
var copilotClient = new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), tokenService.GetToken, logger, "mcs");

await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent:true, cancellationToken:cancellationToken))
{

}
```
