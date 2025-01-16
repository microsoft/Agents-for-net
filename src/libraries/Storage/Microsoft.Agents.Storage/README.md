# Microsoft.Agents.Storage

## About

* A bot is inherently stateless. Once your bot is deployed, it may not run in the same process or on the same machine from one turn to the next. However, your bot may need to track the context of a conversation so that it can manage its behavior and remember answers to previous questions. The state and storage features of the Agents SDK allow you to add state to your bot.
* Microsoft.Agents.State use Storage to persist state to memory, Azure Blobs, or CosmosDb.

## Main Types

- IStorage
- MemoryStorage, which is suitable for testing a bot.  For a production bot, see Microsoft.Agents.Blobs or Microsoft.Agents.CosmosDb.

## Registering MemoryStorage

```
builder.Services.AddSingleton<IStorage, MemoryStorage>();
```
