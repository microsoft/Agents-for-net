# Slack Outbound Parity and Adapter Refactoring

## Goal

Extend the direct Slack transport so message Activities render attachments and suggested actions with the same behavior as Intercom's `SlackMapper.ToSlackMessages`, while keeping the `AgentApplication` and `SlackAgentExtension` contracts unchanged. Refactor `SlackAdapter` into focused internal components and remove `SlackAdapterOptions.BotName`.

The transport remains interchangeable with `CloudAdapter`: agent routes consume and produce the same Activities regardless of whether Slack traffic passes through Azure Bot Service or the direct adapter.

## Architecture

`SlackAdapter` becomes a thin coordinator responsible for:

- Reading and acknowledging Slack HTTP requests.
- Queueing converted Activities for agent processing.
- Dispatching converted outbound messages through the Slack Web API.
- Implementing the `ChannelAdapter` update and delete operations.

The Slack extension registers these internal singleton components:

| Component | Responsibility |
| --- | --- |
| `SlackRequestValidator` | Validate Slack HMAC signatures and request age. |
| `SlackRequestParser` | Distinguish Events API JSON, URL verification, ignored envelopes, and form-encoded interactivity. |
| `SlackEventDeduplicator` | Maintain the bounded ten-minute event ID cache. |
| `SlackActivityConverter` | Convert parsed Slack events and interactive payloads into ABS-compatible Activities. |
| `SlackMessageConverter` | Convert outbound message Activities into one or more Slack message payloads. |
| `SlackFileUploader` | Upload inline attachment content using Slack's current external upload flow. |

`SlackApi` remains the authenticated HTTP boundary and continues sanitized request and response logging.

The existing public `SlackAdapter` constructors remain compatible. `AddSlack` uses an internal factory to compose the registered components and queue dependency.

## Agent Display Name

`SlackAdapterOptions.BotName` is removed.

For each inbound request, `SlackActivityConverter` resolves the bot display name from the runtime `IAgent` type:

1. Use the inherited `AgentAttribute.Name` when present.
2. Otherwise use the short runtime class name.

The converter stores the resolved name in `Activity.Recipient.Name`. Interactive select and button Activities use that recipient name for their bot mention text. The name is carried by the Activity rather than mutable adapter state, allowing multiple agent types to share one adapter safely.

## Outbound Message Conversion

`SlackMessageConverter` accepts a message Activity plus the resolved Slack channel and thread. It returns zero or more immutable Slack message payloads.

For each Activity:

1. Text and converted attachments form the first `chat.postMessage` payload.
2. Suggested actions form a separate threaded message using Intercom's text fallback: one `* value-or-title` line per action.
3. `SlackAdapter.SendActivitiesAsync` sends all generated payloads in order.
4. The Activity's `ResourceResponse` contains the timestamp of the last Slack message, matching Intercom behavior.

Existing text encoding, channel and thread resolution, API token selection, cancellation propagation, and sanitized outbound logging remain unchanged.

### Supported Cards

The converter supports the Intercom card mappings:

- Hero cards.
- Thumbnail cards.
- Audio cards.
- Animation cards.
- Video cards.
- Receipt cards.
- Sign-in cards.
- OAuth cards.

Adaptive Cards are explicitly outside this change because the SDK has no equivalent to Intercom's private Adaptive Card image renderer and attachment store. An Adaptive Card attachment is logged as unsupported and omitted without preventing the remaining Activity content from being sent.

### Card Actions

Card actions map to Slack legacy message attachment actions:

- `imBack`, `postBack`, and `messageBack` become interactive buttons.
- Other action types become Slack-formatted links.
- Interactive actions are split across attachments at Slack's five-actions-per-attachment limit.
- Slack channel data's `render_buttons_as_menu` option remains honored.
- Each generated Slack attachment receives the Activity sender ID as its callback ID so the existing inbound interactive conversion can identify the source.

### Generic Attachments

Generic attachments use their name when supplied and otherwise receive a stable generated name such as `attachment` or `attachment_2`, including a file extension when it can be inferred from the content type.

- Inline byte content and data URLs are uploaded to Slack.
- Ordinary HTTP content and thumbnail URLs are referenced directly.
- Content and thumbnail URLs populate the Slack attachment image, thumbnail, title link, and fallback fields as applicable.

File upload uses Slack's supported sequence:

1. Call `files.getUploadURLExternal`.
2. Upload the binary body to the returned URL.
3. Call `files.completeUploadExternal` with the file ID and destination channel.

The retired `files.upload` method is not used.

## Error Handling

Inbound HTTP behavior remains unchanged:

- Invalid signatures return `401`.
- Invalid Slack JSON returns `400`.
- URL verification returns the challenge.
- Unsupported or ignored envelopes are acknowledged with `200`.
- Queue rejection during host shutdown returns `503` and removes the event from deduplication so Slack may retry.

Slack message-post failures continue to propagate.

Attachment conversion and upload failures are isolated to the individual attachment. The adapter logs the attachment index, content type, and exception, omits that attachment, and sends any remaining text or attachments. Cancellation exceptions are never swallowed. Unsupported Adaptive Cards produce an explicit warning.

If conversion produces no Slack messages, the Activity is a no-op and receives an empty resource response.

## Testing

Tests cover:

- HMAC validation, request parsing, event deduplication, and existing background acknowledgement behavior after extraction.
- ABS-compatible event and interactive Activity conversion.
- `AgentAttribute.Name` and runtime class-name fallback without `SlackAdapterOptions.BotName`.
- Hero, Thumbnail, Audio, Animation, Video, Receipt, Sign-in, and OAuth card conversion.
- Button/link conversion, five-action splitting, and menu rendering.
- Generic inline byte and data URL uploads through the external upload flow.
- Generic content and thumbnail URL rendering.
- Unsupported Adaptive Card logging and omission.
- Partial attachment failure while remaining content is sent.
- Suggested actions as a separate threaded text message.
- Multiple Slack messages sent in order with the last timestamp returned.
- Existing update, delete, logging, streaming, feedback, and Slack route integration behavior.

