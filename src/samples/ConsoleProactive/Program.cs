// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;

// This sample sends a proactive message to an existing conversation WITHOUT an adapter and
// WITHOUT an AgentApplication.  It builds the outbound Activity by hand and posts it using a
// RestConnectorClient directly.
//
// Usage:
//   dotnet run -- <ChannelId> <ClientId> <ClientSecret> <ConversationId> <Text...>

if (args.Length < 5)
{
    Console.WriteLine("Usage: <ChannelId> <ClientId> <ClientSecret> <ConversationId> <Text...>");
    return;
}

var channelId = args[0];
var clientId = args[1];
var clientSecret = args[2];
var conversationId = args[3];
var text = string.Join(' ', args[4..]);

// IHttpClientFactory is the only service we need from DI: MsalAuth and RestConnectorClient
// both create HttpClient instances through it.
var serviceProvider = new ServiceCollection()
    .AddHttpClient()
    .BuildServiceProvider();

// 1. Build a ConversationReference using the Proactive builder.  The ServiceUrl is derived
//    from the channel automatically, and the Agent is set from the client id.
ConversationReference conversationReference = ConversationReferenceBuilder
    .Create(new ChannelId(channelId), conversationId)
    .WithAgent(clientId)
    .Build();

// 2. Create a "message" Activity, set the Text, and apply the ConversationReference so that
//    ChannelId, ServiceUrl, Conversation, From (Agent), and Recipient (User) are populated.
IActivity activity = new Activity(ActivityTypes.Message)
{
    Text = text
}
.ApplyConversationReference(conversationReference);

// Acquires the token used to call the channel service (Azure Bot Service connector).
// A multi-tenant authority is used so a Tenant Id is not required on the command line.
IAccessTokenProvider tokenProvider = new MsalAuth(serviceProvider, new ConnectionSettings()
{
    AuthType = AuthTypes.ClientSecret,
    ClientId = clientId,
    ClientSecret = clientSecret,
    Authority = "https://login.microsoftonline.com/botframework.com",
    Scopes = [AuthenticationConstants.BotFrameworkDefaultScope]
});

// 3. Create a RestConnectorClient directly (no adapter) and post the Activity.
using var connectorClient = new RestConnectorClient(
    new Uri(conversationReference.ServiceUrl),
    serviceProvider.GetRequiredService<IHttpClientFactory>(),
    () => tokenProvider.GetAccessTokenAsync(conversationReference.ServiceUrl, [AuthenticationConstants.BotFrameworkDefaultScope]));

var response = await connectorClient.Conversations.SendToConversationAsync(activity);

Console.WriteLine($"Proactive message sent. Activity id: {response.Id}");
