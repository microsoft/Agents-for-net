# Slack Interactivity Activity Parity

## Goal

Make `SlackAdapter.CreateActivityFromInteractivePayload` produce the same Activity types and payloads as Intercom's `SlackMapper.ToV3Activity`, while preserving the direct Slack transport's existing identities, conversation IDs, channel data, request validation, and background processing.

## Activity Mapping

The adapter examines the first entry in `actions`, matching Intercom behavior.

| Slack payload | Activity mapping |
| --- | --- |
| `interactive_message` or `block_actions` with `feedback_buttons` | Invoke Activity named `message/submitAction`. `Value` contains `actionName`, `actionValue.reaction`, and `replyToId`. `Activity.ReplyToId` is also set because `FeedbackRouteBuilder` reads it from the Activity. |
| `interactive_message` or `block_actions` with `select` | Message Activity whose text is the first selected option value after Slack decoding. |
| `interactive_message` or `block_actions` with `button` | Message Activity whose text is the action value after Slack decoding. |
| `message_action` with a non-empty `callback_id` | Event Activity named `SlackActivity` whose value is `callback_id`. |
| Any other interactive payload | Event Activity named `vnd.slack.action.{payload.type}` whose value is the full parsed payload. |

Select and button Message Activities include a bot mention entity matching Intercom. The mentioned account uses the configured Slack bot identity. `Mention.Text` uses a new optional `SlackAdapterOptions.BotName` value, prefixed with `@`. If `BotName` is not configured, the adapter uses `BotId` so the mention remains populated without breaking existing configurations.

## Common Activity Fields

After selecting the Activity type, the adapter applies the existing direct-transport fields:

- `ChannelId` is Slack.
- `ServiceUrl` is `https://slack.com`.
- `From` is the Slack user and team account ID.
- `Recipient` is the configured Slack bot and team account ID.
- `Conversation` uses bot ID, team ID, channel ID, and message thread timestamp.
- `ChannelData.Payload` preserves the complete parsed Slack payload.
- `ChannelData.ApiToken` contains the configured bot token.
- `Timestamp` remains the direct adapter receipt time.

The change does not modify `AgentApplication`, `SlackAgentExtension`, route attributes, endpoint authentication, or outbound Slack API behavior.

## Model and Configuration Changes

`SlackAdapterOptions` gains optional `BotName` configuration for Intercom-compatible mention text.

`ActionPayload` remains a lossless `SlackModel`. Conversion reads action-specific fields through `SlackModel.Get` so modern Slack composition objects and payload shapes do not require brittle strongly typed action models.

## Error Handling

Missing optional action values produce the same nullable Activity fields that Intercom would produce. Existing validation remains responsible for rejecting payloads without a resolvable team ID. Unknown payload types are not rejected; they become vendor-prefixed Event Activities carrying the full payload.

## Testing

Regression tests cover:

- Feedback buttons invoking `SlackFeedbackLoopRoute`.
- Select actions producing Message Activities with selected text and a bot mention.
- Button actions producing Message Activities with button text and a bot mention.
- Legacy `message_action` producing `Event/SlackActivity`.
- Unknown and actionless payloads producing vendor-prefixed Event Activities with the complete payload.
- Existing feedback, identity, conversation, logging, signature, and background-processing tests remaining green.

