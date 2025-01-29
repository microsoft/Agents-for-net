# Microsoft.Agents.CopilotStudio.Client

Provides a client to interact with agents built in Copilot Studio

This is used when you are creating an application soly for the purpose of user interactive login and will be using a client that will surface an Entra ID MultiFactor Authentication Prompt.

If you are using this client from a service, you will need to exchange the user token used to login to your service for a token for your agent hosted in copilot studio. See here:


### Add the CopilotStudio.Copilots.Invoke permissions to your Application Registration in Entra ID to support user authentication to Copilot Studio

## How-to use

```cs
var copilotClient = new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent:true, cancellationToken:cancellationToken))
{

}
```