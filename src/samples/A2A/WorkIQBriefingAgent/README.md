# Work IQ briefing agent

An end-to-end .NET sample that researches a meeting briefing through **Work IQ
over A2A**, lets the user review and revise it, and **actually sends the approved
email through Microsoft Graph**.

The sample uses the existing Agents SDK A2A extension. Its small, explicit
workflow is sample code, not a new SDK orchestration framework. No separate
model endpoint, model API key, Azure Bot resource, or Copilot Studio agent is
required.

> [!WARNING]
> This is not a simulated send. After `send <approval-code>`, Graph can send real
> email from the signed-in user's mailbox. Start with nonsensitive data and a
> test mailbox you control. Review the recipients, subject, and entire body.

> [!IMPORTANT]
> The A2A extension is in preview. Build this sample with this repository's
> source projects and pinned dependency versions.

## The scenario

Ask: "Prepare a briefing for my Contoso meeting tomorrow. Include recent
discussions, outstanding commitments, and risks."

```mermaid
sequenceDiagram
    actor User
    participant CLI as A2ACli
    participant Agent as Briefing agent
    participant WorkIQ as Work IQ
    participant Graph as Microsoft Graph
    User->>CLI: Request a briefing
    CLI->>Agent: Authenticated A2A task
    Agent->>WorkIQ: Delegated research/drafting request
    WorkIQ-->>Agent: Progress, task/context IDs, briefing
    Agent-->>CLI: Markdown artifact and input-required
    User->>CLI: Choose recipients / request a revision
    CLI->>Agent: Continue the same outer task
    Agent->>WorkIQ: Follow-up using the Work IQ context
    Agent-->>CLI: Exact email preview and fresh approval code
    User->>CLI: send <current-code>
    CLI->>Agent: Explicit approval
    Agent->>Agent: Save sending marker before the side effect
    Agent->>Graph: POST /me/sendMail using delegated Mail.Send
    Graph-->>Agent: 202 Accepted
    Agent-->>CLI: Accepted for sending; complete the A2A task
```

Work IQ can ask a clarification question. The agent returns `input-required`
and sends the next answer to that same downstream task. After Work IQ completes,
revisions reuse its `contextId` but do not attempt to reopen its completed task.
The outer briefing task stays open until sending or cancellation.

## Read the code in this order

| File | Responsibility |
| --- | --- |
| [BriefingAgent.cs](BriefingAgent.cs) | The readable conversation flow: research, review, revision, approval, send, and cancellation. |
| [BriefingWorkflow.cs](BriefingWorkflow.cs) | Validate explicit recipients, bind approval to the exact email, and checkpoint before sending. |
| [BriefingState.cs](BriefingState.cs) | Task state, including the separate Work IQ IDs and send outcome. Tokens are never stored here. |
| [WorkIqClient.cs](WorkIqClient.cs) | Authenticated discovery, A2A 1.0, streaming projection, task observation, and cancellation. |
| [GraphMailSender.cs](GraphMailSender.cs) | The actual Graph HTTP request. There is deliberately no send retry policy. |
| [SignedInUser.cs](SignedInUser.cs), [UserTaskStore.cs](UserTaskStore.cs) | Partition task retrieval, listing, and cancellation by validated tenant/user identity. |
| [BriefingAgentStartup.cs](BriefingAgentStartup.cs) | Hosting, required authentication, service clients, in-memory storage, and telemetry. |

Comments at the protocol and authorization boundaries explain why those steps
exist. For the extension itself, see the
[A2A developer guide](../A2AAgent/A2A-DEVELOPER-GUIDE.md).

## Prerequisites and consent

- A .NET SDK that builds `net10.0`.
- A work/school tenant with [Work IQ enabled](https://learn.microsoft.com/microsoft-365/copilot/extensibility/work-iq/enable-work-iq).
- The required Work IQ access and [Copilot Credits billing](https://learn.microsoft.com/microsoft-365/copilot/extensibility/work-iq/api-overview#licensing-requirements).
- A signed-in user with an Exchange Online mailbox.
- Permission to register an Entra application and obtain the required consent.

Configure one **single-tenant Entra app registration** using the same
public-client/OBO pattern as the existing authenticated A2A sample:

1. In the app manifest, set `api.requestedAccessTokenVersion` to `2`.
2. Under **Expose an API**, set the Application ID URI to
   `api://botid-<agent-client-id>` and expose a delegated scope named
   `access_as_user`.
3. Add these **delegated**, not application, API permissions and grant the
   consent required by your tenant:

   | Resource | Permission |
   | --- | --- |
   | Work IQ, `api://workiq.svc.cloud.microsoft` | `WorkIQAgent.Ask` (admin consent required) |
   | Microsoft Graph | `Mail.Send` |

4. Under **Authentication**, enable **Allow public client flows** for Device
   Code sign-in. The existing A2ACli uses this agent app's client ID for this
   sample; do not select its application-only `a2aclient` registration.
5. Create a client secret for the agent's confidential OBO exchange.

The three credentials have different purposes:

| Hop | Token audience / scope |
| --- | --- |
| A2ACli to this agent | Agent API, `api://botid-<agent-client-id>/access_as_user` |
| This agent to Work IQ | `api://workiq.svc.cloud.microsoft/WorkIQAgent.Ask` |
| This agent to Graph | `https://graph.microsoft.com/Mail.Send` |

The `workiq` and `mail` authorization handlers exchange the validated incoming
agent token separately. Both handlers are declared on the skill, so sign-in and
consent are resolved before the workflow runs. Never send a Graph or Work IQ
token as the credential for the briefing agent.

> [!IMPORTANT]
> `WorkIQAgent.Ask` is **not read-only**: its documented permission includes
> access to actions available to Work IQ agents. This sample asks Work IQ for
> research/drafting only, but that prompt is not a security policy.
> The approval gate controls **this sample's Graph send**, not tools or actions
> inside Work IQ. See the [Work IQ permissions reference](https://learn.microsoft.com/microsoft-365/copilot/extensibility/work-iq/permissions).
> Plain-text output also does not automatically inherit source document labels
> or sharing restrictions.

## Configure without committing credentials

Run the following PowerShell from the repository root. Replace the two IDs;
the secret is entered through a masked prompt and passed to the local .NET
user-secrets tool through standard input, not a command-line argument.

```powershell
$project = 'src\samples\A2A\WorkIQBriefingAgent\WorkIQBriefingAgent.csproj'
$tenantId = '<your-tenant-guid>'
$clientId = '<your-agent-app-client-guid>'
$settings = (Get-Content 'src\samples\A2A\WorkIQBriefingAgent\appsettings.json' -Raw).
    Replace('{{TenantId}}', $tenantId).Replace('{{ClientId}}', $clientId) |
    ConvertFrom-Json
$settings.WorkIQ.TimeZone = 'UTC'
$settings.Connections.ServiceConnection.Settings.ClientSecret =
    [System.Net.NetworkCredential]::new('', (Read-Host 'Agent app client secret' -AsSecureString)).Password
$settings | ConvertTo-Json -Depth 20 | dotnet user-secrets set --project $project
$settings.Connections.ServiceConnection.Settings.ClientSecret = $null
```

Choose an appropriate IANA time zone, such as `America/New_York`, instead of
`UTC` when using relative dates. This value and its current offset are sent in
Work IQ's `Location` metadata; the sample does not guess the user's time zone.

User secrets are local development configuration, not an encrypted production
vault. The supplied launch profile selects Development so ASP.NET Core loads
them. Keep credentials out of tracked settings and terminal transcripts.

## Run and send a real email

Start the agent:

```powershell
dotnet run --project src\samples\A2A\WorkIQBriefingAgent\WorkIQBriefingAgent.csproj
```

In another terminal, from the repository root:

```powershell
dotnet run --project src\samples\A2A\A2ACli\A2ACli.csproj -- `
  --agent http://localhost:3978/a2a --agent-client-id <your-agent-app-client-guid>
```

Choose `y` for streaming. Leave authentication in automatic mode; the client
discovers the skill's delegated OAuth requirements. Complete the Device Code
sign-in when prompted. At each attachment prompt, press Enter to skip.

Use this conversation, replacing the test address and copying the actual
approval code from the latest preview:

```text
Prepare a briefing for my Contoso meeting tomorrow. Include commitments and risks.

to <a-test-mailbox-you-control>

revise Focus on the outstanding commitments and shorten the background.

review

send <the-current-12-character-code-shown-by-the-agent>
```

The preview shows the exact recipients, fixed subject `Meeting briefing`, and
body. The body is the briefing's Markdown text, sent as a **plain-text email**.
The same content is returned in the A2A artifact named `briefing.md`.

Each revision or recipient change invalidates the previous approval. `review`
issues a fresh code that expires after 15 minutes. A bare `send`, an old code,
or a model-generated address cannot bypass this review.

Approval means explicit confirmation from the authenticated caller, not proof
of human presence. If another agent calls this sample, its client must present
the preview and collect user consent rather than letting an LLM approve itself.

The successful response says **accepted for sending**, not delivered.
[Graph returns `202 Accepted`](https://learn.microsoft.com/graph/api/user-sendmail)
without a delivery receipt. Check the sender's Sent Items and manually confirm
receipt at the designated test mailbox to complete the live walkthrough.

### Task controls

| Command | Meaning |
| --- | --- |
| `to alice@example.com; bob@example.com` | Explicitly choose recipients; invalidates approval. |
| `revise <instructions>` | Ask Work IQ to revise using its existing context; invalidates approval. |
| `review` | Reprint the exact email and issue a new approval code. |
| `send <code>` | Approve and initiate the real Graph send. |
| `cancel` | Cancel the briefing workflow before sending. It cannot recall mail. |
| `resume` | Ask this agent to retrieve/subscribe to its known **downstream Work IQ task**, without resubmitting research. |
| `:status` | A2ACli retrieves its last **outer briefing task**, including the task ID and status. |
| `:resume` | A2ACli resubscribes to the last outer task's stream; does not repeat the original request. |
| `:cancel` | A2ACli sends the A2A cancellation operation for the last outer task. |

CLI commands are entered between turns. Ctrl+C ends the CLI; it is not an email
recall operation. Cancelling an active research request asks for downstream
cancellation on a separate short deadline; failure to confirm cancellation is
logged without including content or tokens. Stopping a task-observation
subscription only stops observing; use a cancellation command to cancel work.

## Why the send marker matters

The sample clears approval and saves `Sending` **before** calling Graph. The SDK
conversation store's optimistic concurrency rejects a stale competing write
before it can send. A repeated confirmation does not initiate another send.

If the connection drops after submission, the email might already have been
accepted. The workflow records `Unknown`, does not retry, and tells the user to
check the mailbox. This is intentionally conservative; it is not an
exactly-once email delivery guarantee.

Both approval state and native A2A tasks use **in-memory storage**. Restarting
the sample loses them. Task observation and resubscription are not durable
execution, crash recovery, or a scheduler. For production, add durable storage,
retention, distributed coordination, and an appropriate recovery/reconciliation
policy before introducing background workers.

Authentication remains enabled in Development. The public Agent Card contains
discovery/OAuth metadata only; task operations require the signed-in user and
are partitioned by validated tenant and user IDs. Use HTTPS beyond local
loopback. Prompts, email bodies, and tokens are not deliberately logged by the
sample. Existing sample OTLP telemetry includes `A2A`, `A2A.AspNetCore`, and
`WorkIQBriefingAgent` activity sources and task correlation.

## Validate

```powershell
dotnet build src\samples\A2A\WorkIQBriefingAgent\WorkIQBriefingAgent.csproj
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj
```

Automated tests use controlled HTTP responses for Work IQ and Graph. They
exercise the actual clients and A2A processing, approval invalidation, unknown
send outcomes, cancellation, and user/tenant isolation. They never send email
and do not prove access to a live tenant. The live conversation above is the
separate end-to-end acceptance check.

If setup fails:

- `AADSTS7000218`: enable public client flows on the agent app registration.
- Consent or OBO errors: verify delegated permissions, the agent credential,
  and that the CLI uses the agent app client ID.
- Work IQ authentication errors: sign in through the user's home tenant and
  verify Work IQ enablement and billing.
- `401` from the agent: check the token audience, tenant, and v2 access-token
  configuration. Do not disable validation to work around it.
