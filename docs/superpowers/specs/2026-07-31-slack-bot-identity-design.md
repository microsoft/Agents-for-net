# Slack Bot Identity Design

## Goal

Make activities produced by the direct `SlackAdapter` use the same Slack identity
representation as the Azure Bot Service Slack channel. This preserves
`AgentApplication` behavior when switching between direct Slack ingestion and
`CloudAdapter`.

## Configuration

`SlackAdapterOptions` will expose two distinct Slack identities:

- `BotId`: the Slack bot ID from `users.info.user.profile.bot_id`, typically
  beginning with `B`.
- `BotUserId`: the OAuth bot user ID from `bot_user_id`, typically beginning
  with `U`.

`BotUserId` remains responsible for detecting events authored by the bot user.
`BotId` is used for the Activity recipient and conversation identity.

## Activity Mapping

For an event from team `T1`, channel `C1`, user `U1`, and bot `B1`, the adapter
will produce:

- `Activity.From.Id`: `U1:T1`
- `Activity.Recipient.Id`: `B1:T1`
- Top-level `Activity.Conversation.Id`: `B1:T1:C1`
- Threaded `Activity.Conversation.Id`: `B1:T1:C1:<thread_ts>`

The same mapping applies to Events API and interactivity payloads. Display names
remain unset because direct Slack payloads do not provide the user and bot names
required to populate them without additional Web API calls.

The adapter will ignore an event when its Slack `user` equals `BotUserId`.
Existing `bot_id` detection remains as a broader guard against bot-authored
messages.

## Helpers

`SlackHelpers` will provide Slack account ID encoding and decoding for the
`<slack-id>:<team-id>` format, following the existing conversation-ID helper
pattern. Activity construction will use these helpers rather than duplicate
string formatting.

## Compatibility

Adding `BotId` is additive. Existing `BotUserId` configuration retains its
documented OAuth user identity meaning. Applications must configure `BotId` to
obtain Azure Bot Service-compatible recipient and conversation identities.

No `AgentApplication` or `SlackAgentExtension` APIs or behavior will change.

## Tests

Adapter tests will use distinct `B...` and `U...` values and verify:

- Events API activities encode sender, recipient, and conversation identities.
- Interactivity activities use the same identity representation.
- Events whose user equals `BotUserId` are ignored.
- Bot-authored events containing `bot_id` remain ignored.
- Threaded and top-level conversation IDs retain their established shapes.

Helper tests will cover account ID encoding and decoding, including invalid
encoded IDs.
