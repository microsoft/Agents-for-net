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
//   dotnet run -- [--tenant <TenantId>] <ChannelId> <ClientId> <ClientSecret> <ConversationId> <Text...>

// Extract the optional "--tenant <TenantId>" flag before parsing positional arguments so it
// can appear anywhere on the command line (the trailing Text is variadic).
string? tenantId = null;
var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if ((args[i] == "--tenant" || args[i] == "-t") && i + 1 < args.Length)
    {
        tenantId = args[++i];
    }
    else
    {
        positional.Add(args[i]);
    }
}

if (positional.Count < 5)
{
    Console.WriteLine("Usage: [--tenant <TenantId>] <ChannelId> <ClientId> <ClientSecret> <ConversationId> <Text...>");
    return;
}

var channelId = positional[0];
var clientId = positional[1];
var clientSecret = positional[2];
var conversationId = positional[3];
var text = string.Join(' ', positional.Skip(4));

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
// When a Tenant Id is supplied a single-tenant authority is used; otherwise the multi-tenant
// "botframework.com" authority is used.
var authority = string.IsNullOrEmpty(tenantId)
    ? "https://login.microsoftonline.com/botframework.com"
    : $"https://login.microsoftonline.com/{tenantId}";

IAccessTokenProvider tokenProvider = new MsalAuth(serviceProvider, new ConnectionSettings()
{
    AuthType = AuthTypes.ClientSecret,
    ClientId = clientId,
    ClientSecret = clientSecret,
    Authority = authority,
    Scopes = [AuthenticationConstants.BotFrameworkDefaultScope]
});

// 3. Create a RestConnectorClient directly (no adapter) and post the Activity.
using var connectorClient = new RestConnectorClient(
    new Uri(conversationReference.ServiceUrl),
    serviceProvider.GetRequiredService<IHttpClientFactory>(),
    () => tokenProvider.GetAccessTokenAsync(conversationReference.ServiceUrl, [AuthenticationConstants.BotFrameworkDefaultScope]));

var response = await connectorClient.Conversations.SendToConversationAsync(activity);

Console.WriteLine($"Proactive message sent. Activity id: {response.Id}");
