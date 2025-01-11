# Microsoft.Agents.Client

Contains the implementation for bot-to-bot handling.

## Main Types

- IChannel
- IChannelHost
- IConversationIdFactory
- HttpBotChannel: Implements IChannel for sending requests to another Agent using Http.
- ConversationIdFactory: IConversationIdFactory that uses IStorage for persistence.
- ConfigurationChannelHost : IChannelHost that loads Channel definitions from configuration.