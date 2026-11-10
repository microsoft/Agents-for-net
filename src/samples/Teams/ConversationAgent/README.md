
# Teams Conversation Bot

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents). This sample shows how to incorporate basic conversational flow into a Teams application. It also illustrates a few of the Teams specific calls you can make from your bot.

## Prerequisites

-  Microsoft Teams is installed and you have an account (not a guest account)
-  [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) version 10.0, matching this sample's target framework
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
-  [M365 developer account](https://docs.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/prepare-your-o365-tenant) or access to a Teams account with the appropriate permissions to install an app.

## Running this sample

The sample uses project references to the SDK in this checkout, so its validation
commands exercise local changes rather than an older published Agents package.
Keep incoming token validation enabled when connecting Teams. Anonymous access to
the dev tunnel does not mean that agent authentication should be disabled.

For local development, store credentials outside the repository. From the repository
root, configure the existing single-tenant connection with user secrets:

```powershell
$project = ".\src\samples\Teams\ConversationAgent\ConversationAgent.csproj"
dotnet user-secrets set "Connections:ServiceConnection:Settings:ClientId" "<bot-app-id>" --project $project
dotnet user-secrets set "Connections:ServiceConnection:Settings:AuthorityEndpoint" "https://login.microsoftonline.com/<tenant-id>" --project $project
dotnet user-secrets set "Connections:ServiceConnection:Settings:ClientSecret" "<client-secret>" --project $project
dotnet user-secrets set "TokenValidation:Audiences:0" "<bot-app-id>" --project $project
dotnet user-secrets set "TokenValidation:TenantId" "<tenant-id>" --project $project
```

The launch profile uses `Development`, where ASP.NET loads these secrets. Do not
commit real credentials into either appsettings file. If you have not configured
the original sample's optional `graph` OAuth sign-in connection, turn off its
automatic user sign-in while running the validation commands:

```powershell
dotnet user-secrets set "AgentApplication:UserAuthorization:AutoSignin" "false" --project $project
```

This does not disable incoming JWT validation or grant Graph file permissions.
The existing setup steps below describe Azure Bot, manifest, and tunnel setup;
leave credential placeholders in tracked configuration when using user secrets.

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Be sure to add the Teams Channel
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{ClientId}}", // this is the Client ID used for the connection.
            "ClientSecret": "{{ClientSecret}}", // this is the Client Secret used for the connection.
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      },
      ```

      1. Set the corresponding user-secret keys above to the Azure Bot's app ID and tenant ID.
      1. Store the app-registration client secret in user secrets, not in this tracked JSON file.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     -  Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent, and select **Preview in Teams** in the upper right corner

   From the repository root, the explicit run command is:

   ```powershell
   dotnet run --project .\src\samples\Teams\ConversationAgent\ConversationAgent.csproj --launch-profile ConversationAgent
   ```

## Live validation of Teams integration changes

The manifest now enables `supportsFiles` and adds **validate** to the existing
command list. Repackage and reinstall/update the Teams app after changing the
manifest; restarting the server alone does not update the installed manifest.
The sample uses the stable v1.29 manifest for targeted messages and slash commands.
The validation additions do not enable threading, Plans, HTML widgets, or
Socket Mode.

Start in the bot's **personal chat**. Type `validate` for the available checks.
In group/channel conversations, mention the bot so Teams delivers the message;
the sample's existing recipient-mention removal handles the command text.
Use small, non-sensitive fixtures, not production documents.
Validation replies to private targeted prompts remain targeted to the sender.
Run streaming checks in a normal bot chat; targeted prompts receive a private
instruction instead of starting a conversation-wide stream.

### Incoming attachments

A normal, non-targeted attachment message without another command produces a
plain-text report instead of the welcome card. It shows the incoming MIME/name, whether `ContentUrl` exists,
whether metadata declares `downloadUrl`, and the shared downloader's resulting
filename, MIME type, byte count, and SHA-256 digest. It does not display locator
values, signed query strings, tokens, file contents, or account identifiers.
Only the first ten incoming/downloaded entries are shown.

`Program.cs` already registers `AddAgentM365AttachmentDownloader()`. All downloads
still run through shared `M365AttachmentDownloader` and arrive in
`turnState.Temp.InputFiles`; this sample does not add another downloader or fetch
files itself.

Run this matrix on the Teams desktop and web clients available to you:

| Action | Observe in the report | Expected check |
| --- | --- | --- |
| Upload a small `.txt` using the paperclip / upload-from-device UI in personal chat | File-download metadata, normally `downloadUrl declared: True` | Original filename, downloaded bytes and digest match the fixture. |
| Drag/drop a file or image into the compose box | The actual attachment shape chosen by that client | If sent as an uploaded file, validate its original name and bytes; do not assume dragging an image means a Bot API inline image. |
| Paste a clipboard screenshot/image into the compose box | `image/*`, a content URL, and possibly a separate `text/html` companion | Downloaded image MIME/name reflect received bytes; the HTML companion is not an input file. |
| Paste/copy PNG, JPEG, GIF, or WebP images where the client supports them | The received rather than original image format | Teams may transform the image, for example GIF to PNG. The downloaded extension/MIME must match received bytes, not necessarily the original filename or digest. |
| Send several supported files and images in one message | Incoming count, downloaded count, and each result | Each supported input appears once. HTML companions or skipped inputs can make the counts differ. |
| Attach a OneDrive/SharePoint file using Teams' cloud-file UI | Signed URL metadata, unsigned file metadata, or no attachment, depending on client/identity | Signed metadata follows the ordinary upload path. Unsigned metadata requires the agentic-user setup below. |
| Paste a plain sharing link as message text | Usually no attachment is delivered | This is not a file-download test. Normal text retains the welcome behavior; `validateattachments` reports only its own current turn. |

For an unchanged uploaded file, compare the reported digest with:

```powershell
Get-FileHash -LiteralPath .\test-file.txt -Algorithm SHA256
```

`validateattachments` can also be used as an attachment caption where supported.
Reports are **per activity**, not a history of previously uploaded files. For an
empty/skipped result, check the input shape and downloader warnings, not just
whether the filename looks correct. Do not enable HTTP body/header tracing or
share raw attachment payloads: signed URLs are credentials.

Teams controls which payload each UI action sends. There is no reliable client
button for forcing a missing `ContentUrl`, an expired signed URL, or a redirect
downgrade. Use automated downloader tests for controlled payloads, HTTP responses,
and credential checks. HTTPS-to-HTTP redirect enforcement is coordinated separately
in [#1078](https://github.com/microsoft/Agents-for-net/pull/1078), not demonstrated
by this sample. A live successful upload does not prove credential isolation or
every negative path.
Ordinary bot file-upload support is personal-chat-specific; do not interpret a
missing classic-bot group/channel attachment as a downloader failure.

### Agentic-user OneDrive/SharePoint files

Unsigned `application/vnd.microsoft.teams.file.download.info` metadata, without
`downloadUrl`, is a separate Graph test. It requires a real `agenticUser`
recipient with tenant/agent/user identity metadata, a configured connection
implementing `IAgenticTokenProvider.GetAgenticUserTokenAsync`, and file/site Graph
permissions. The file must also be shared with that user. A normal Azure Bot
with a client secret or the sample's optional delegated `graph` OAuth sign-in
does not by itself establish these prerequisites.

Use the [shared downloader guide](../../../libraries/Builder/Microsoft.Agents.Builder/README.md#agentic-user-shared-files)
for identity, scopes, consent diagnostics and sovereign endpoint configuration.
The [A365LoopTest sample](../../A365LoopTest/README.md) provides additional agentic
hosting context. Keep the existing M365 downloader registration; configure its
`GraphBaseUrl` only if a non-default Graph endpoint is needed, and allowlist that
host when outbound validation is enabled.

With that identity, test personal, group-chat and channel scopes where the
platform actually delivers unsigned file metadata. Inspect the reported recipient
role/scope and confirm that the original filename and downloaded bytes arrive.
If these prerequisites are unavailable, record this row as **not tested**, not as
a passed Graph scenario. Do not manufacture a role or inject sharing URLs into an
ordinary bot conversation to claim a live agentic test.

### Other validation commands

| Command | Expected result |
| --- | --- |
| `validateconversion` | Conversation, sender and recipient ID comparisons say `PASS` when those fields exist. IDs are compared, not printed. |
| `validatemarkdown` | A heading, checklist and table render using `extendedmarkdown`. Rendering depends on the receiving client; no HTML widgets are involved. |
| `validatelabel` | The configured Purview label appears on a non-AI-marked message. An unset/invalid label ID produces setup instructions. |
| `validatestream` | An informative update and text are followed by a final hero card and optional sensitivity label. |
| `validatemeeting` | In a meeting activity with meeting channel data, typed metadata resolves and reports whether an ID exists. Ordinary chats correctly report no meeting metadata. |
| `validatefinaltimeout` | With fault injection explicitly enabled, the sample checks an actual final send and recovery update as described below. |

To test a sensitivity label, use an actual label GUID configured for the tenant,
then restart the sample:

```powershell
$project = ".\src\samples\Teams\ConversationAgent\ConversationAgent.csproj"
dotnet user-secrets set "TeamsValidation:SensitivityLabelId" "<actual-purview-label-guid>" --project $project
```

A fallback display name is not proof of tenant label resolution. Inspect the
actual label in Teams. If the tenant/client cannot resolve or display it, record
that limitation separately from SDK serialization.

### Final-send recovery: opt-in simulated error, real Teams update

Enable this only in a test deployment, restart the sample, then send
`validatefinaltimeout`:

```powershell
$project = ".\src\samples\Teams\ConversationAgent\ConversationAgent.csproj"
dotnet user-secrets set "TeamsValidation:EnableFinalSendFault" "true" --project $project
```

The sample sends through the real adapter first, so Teams receives the final
activity. A per-turn send hook then injects the recognized service-timeout error.
This models the recovery branch without a ten-minute wait and leaves Teams with
a finalized activity that can be updated. The SDK performs a real update to that
original activity, using the complete final text/card and configured label.

The final report says `PASS` only when the error was injected, the update
succeeded, the composed payload was retained without streaming markers or
duplicate attachments, and the SDK returned `Success`. A rejected update or
incomplete payload reports `FAIL`. Also inspect the visible text/card/label.

**This is fault injection, not a Teams-generated timeout.** It does not prove the
service's ten-minute window, network-failure timing, or tenant/client rendering.
The genuine service lifetime is separate from the SDK's two-minute queue-drain
timeout. Turn the simulation off afterward:

```powershell
dotnet user-secrets set "TeamsValidation:EnableFinalSendFault" "false" --project $project
```

### Automated sample checks

The focused checks use in-memory adapters and existing SDK hooks; they require
no Azure credentials and are not live Teams tests:

```powershell
dotnet test .\src\tests\Microsoft.Agents.SampleTest\Microsoft.Agents.SampleTest.csproj --filter "FullyQualifiedName~TeamsConversationValidationTests"
```

They verify safe attachment reporting, Teams-channel command routing, disabled
fault injection by default, conversion/configuration feedback, and both
successful and failed final recovery. They also check that the new commands do
not broadcast responses to private targeted prompts.

## Interacting with this Agent in Teams

You can interact with this bot in Teams by sending it a message, or selecting a command from the command list. The bot will respond to the following strings.

1. **Show Welcome**
   - **Result:** The bot will send the welcome card for you to interact with
   - **Valid Scopes:** personal, group chat, team chat
2. **MentionMe**
   - **Result:** The bot will respond to the message and mention the user
   - **Valid Scopes:** personal, group chat, team chat
3. **MessageAllMembers**
   - **Result:** The bot will send a 1-on-1 message to each member in the current conversation (aka on the conversation's roster).
   - **Valid Scopes:** personal, group chat, team chat
4. **QuotedReply**
   - **Result:** The bot replies with a `quotedReply` entity that references the incoming message.
   - **Valid Scopes:** personal, group chat, team chat
5. **TargetedMessages:**
   - **Result:** The bot sends a separate targeted message to each member, visible only to that member.
   - **Valid Scopes:** personal (user-to-user) chat, group chat, team chat
5. **PromptPreview** _(Type `/` in the Teams compose box and select this agent)_
   - **Result:** The bot sends a targeted response containing Prompt Preview metadata.
   - **Valid Scopes:** personal (user-to-user) chat, group chat, team chat
   - **Usage:** Select `promptpreview` from the Teams slash-command picker. This uses the stable v1.29 manifest included with the sample.
6. **CustomFeedback**
   - **Result:** The agent sends a response with thumbs-up and thumbs-down buttons. Selecting either button opens a custom feedback dialog.
   - **Valid Scopes:** personal, group chat, team chat

### Custom feedback flow

The `customfeedback` response calls `TeamsEnableFeedbackLoop("custom")`, which sets `channelData.feedbackLoop.type` to `custom`. This tells Teams to request a custom dialog instead of showing the default feedback form.

When the user selects thumbs up or thumbs down, Teams sends a `message/fetchTask` invoke activity. `OnCustomFeedbackFetchAsync` responds with a small dialog containing an Adaptive Card text input. When the user submits the dialog, Teams sends a `message/submitAction` invoke with `actionName` set to `feedback`. The `[TeamsFeedbackLoopRoute]` handler receives the reaction and the form data. This sample logs the reaction and whether additional feedback was entered, without logging the feedback text; a production application should validate and persist the feedback in its own data store.

Teams does not store custom feedback for the application. For protocol details, see [Bot messages with AI-generated content](https://learn.microsoft.com/microsoftteams/platform/bots/how-to/bot-messages-ai-generated-content#feedback-buttons).

You can select an option from the command list by typing ```@TeamsConversationBot``` into the compose message area and ```What can I do?``` text above the compose area.

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
