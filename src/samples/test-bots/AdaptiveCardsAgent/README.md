# Typeahead Bot

This sample shows how to incorporate the typeahead search functionality in Adaptive Cards into a Microsoft Teams application using [Bot Framework](https://dev.botframework.com) and the Teams AI SDK. Users can search nuget.org for packages.

## Set up instructions

All the samples in the C# .NET SDK can be set up in the same way. You can find the step by step instructions here: [Setup Instructions](../README.md).

## Interacting with the bot

![Typeahead search](./assets/TypeaheadSearch.png)

Send "static" to get the Adaptive Card with static typeahead search control and send "dynamic" to get the Adaptive Card with dynamic typeahead search control.

**static search**: Static typeahead search allows users to search from values specified within `Input.ChoiceSet` in the Adaptive Card payload.

**dynamic search**: Dynamic typeahead search is useful to search and select data from large data sets. The data sets are loaded dynamically from the `dataset` specified in the Adaptive Card payload.

On clicking "Submit" button, the bot will return the choices that have been selected.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases) for Testing Web Chat.

## Running this sample

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure Entra ID Application Identities
    - Create and configure an [Azure Bot Service](https://aka.ms/AgentsSDK-CreateBot) for your bot
    - Create and configure an [Azure App Service](https://learn.microsoft.com/azure/app-service/) to deploy your bot on to.
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.

## Further reading

- [Teams Toolkit overview](https://aka.ms/vs-teams-toolkit-getting-started)
- [How Microsoft Teams bots work](https://learn.microsoft.com/azure/bot-service/bot-builder-basics-teams?view=azure-bot-service-4.0&tabs=csharp)
