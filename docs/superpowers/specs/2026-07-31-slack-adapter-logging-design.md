# Slack Adapter Logging Design

## Goal

Add CloudAdapter-style diagnostic logging to `SlackAdapter` for verified inbound
Slack payloads, converted Activities, and outbound adapter responses without
exposing Slack credentials or callback URLs.

## Logging Architecture

Add an internal `SlackAdapterLog` partial class that uses source-generated
`LoggerMessage` methods and a stable event ID registry. Payload logging is
enabled at `Debug` level, matching `CloudAdapterLog`.

`SlackAdapter` remains the logging boundary. Calls made directly through
`SlackAgentExtension`, `SlackApi`, or Slack streaming helpers are outside this
change.

## Inbound Flow

After the request signature is verified, log the received Slack payload in
sanitized form. Invalid signatures continue to emit only the existing warning;
their untrusted request bodies are not logged.

For Events API requests, log sanitized JSON. For form-encoded interactivity
requests, extract the `payload` JSON and log its sanitized representation.
Challenge, unsupported-envelope, duplicate, and bot-authored requests may still
be logged as received, even when they do not produce an Activity.

After an Events API or interactivity payload is converted, log the resulting
Activity immediately before it enters `ProcessActivityAsync`. Payloads that are
acknowledged without an Activity do not emit an Activity log.

## Outbound Flow

Log adapter responses only when `SlackAdapter` performs a Slack Web API call:

- `SendActivitiesAsync`: log each message Activity sent through
  `chat.postMessage`.
- `UpdateActivityAsync`: log the updated Activity sent through `chat.update`.
- `DeleteActivityAsync`: log the conversation and activity reference sent
  through `chat.delete`.

The successful Slack response timestamp is included as structured metadata.
Typing, trace, empty-text, and other no-op Activities are not logged as sent.
Failed Web API calls continue to throw and therefore do not emit a successful
response log.

## Redaction

All payload and Activity bodies pass through one JSON redaction helper before
logging. Property matching is case-insensitive and covers credential or callback
fields including:

- `token`
- `api_token`
- `access_token`
- `bot_access_token`
- `authorization`
- `signing_secret`
- `response_url`

Matching property values are replaced with `[REDACTED]`. Redaction recursively
processes nested objects and arrays. This removes `SlackChannelData.ApiToken`
from serialized Activities and handles Slack payload variants without mutating
the request, Activity, or outbound object.

If text is not valid JSON, the logger emits `[UNAVAILABLE]` rather than the raw
text. This prevents malformed input from bypassing redaction.

## Events

Use a dedicated event ID range within `SlackAdapterLog`:

- 1: verified Slack payload received
- 2: Slack Activity created
- 3: Slack message sent
- 4: Slack message updated
- 5: Slack message deleted

Messages include structured request, event, conversation, channel, and Slack
timestamp values where available. Full sanitized bodies remain one structured
string field, following `CloudAdapterLog` Activity logging.

## Testing

Tests will use a recording or mocked `ILogger<SlackAdapter>` and verify:

- Verified Events API and interactivity payloads emit inbound logs.
- Invalid signatures do not log their payload.
- Converted Activities emit Activity logs.
- Send, update, and delete emit their respective successful response logs.
- No-op Activities do not emit sent-response logs.
- Tokens, authorization values, signing secrets, and response URLs are absent
  from all logged messages and replaced by `[REDACTED]`.
- Malformed non-JSON input is represented as `[UNAVAILABLE]`.

Existing Slack conversion, signature verification, routing, and response
behavior remain unchanged.
