# AgentToAgent

This Sample contains two Agents, Agent1 and Agent2, that represent the two legs of a multi-agent communication sequence using Agents SDK. Each Agent has its own read me to setup and configure the Agent.  

Found here:

Its a good idea to individually test each Agent to make sure its alive before proceeding.

## Key concepts in this sample

The solution includes a parent (`Agent1`) and another Agent (`Agent2`) and shows how the parent agent can send Activities to another and return the responses to the user.

- `Agent1`: this project shows how to consume an echo skill and includes:
  - A [HostAgent](Agent1/HostAgent.cs) that calls the Agent2.Echo and keeps the conversation active until the user says "end". [HostAgent](Agent1/HostAgent.cs) also keeps track of the conversation with the skill and handles the `EndOfConversation` activity received from the Agent2 to terminate the conversation
  - A [ChannelResponseController](Agent1/ChannelResponseController.cs) that handles responses
- `Agent2`: this project shows a simple Agent that receives message activities from the caller and echoes what the user said.

## Testing the Agent using Emulator

[Bot Framework Emulator](https://github.com/microsoft/botframework-emulator) is a desktop application that allows developers to test and debug their Agent on localhost or running remotely through a tunnel.

- Install the Bot Framework Emulator version 4.7.0 or greater from [here](https://github.com/Microsoft/BotFramework-Emulator/releases)

### Connect to the Agent using Bot Framework Emulator

- Launch Bot Framework Emulator
- File -> Open Bot
- Enter a Bot URL of `http://localhost:3978/api/messages`

