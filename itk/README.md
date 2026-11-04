# A2A integration checks (manual, non-gating)

This small agent exercises `Microsoft.Agents.Extensions.A2A` through the official
[A2A Integration Test Kit](https://github.com/a2aproject/a2a-itk). It supplements,
but does not replace, the full protocol suites already run by
[a2a-dotnet](https://github.com/a2aproject/a2a-dotnet/tree/main/itk).
The selected paths test the Agents SDK's Agent Card composition, endpoint mapping,
incoming protobuf file-part conversion, `AgentApplication` route execution, task
state, and an outbound call to a peer through the a2a-dotnet client.
The vendored [instruction wire schema](instruction.proto) comes from a2a-itk;
its Apache 2.0 license is included in [A2A-ITK-LICENSE.txt](A2A-ITK-LICENSE.txt).

## Coverage and boundaries

| Suite | Selected coverage | Not claimed |
| --- | --- | --- |
| ITK | v1.0 Python peer, JSON-RPC and HTTP+JSON, non-streaming `send_message` in both directions | gRPC, v0.3, streaming, push notifications, resubscription |
| ACTS | Official Agent Card discovery checks, plus focused completed-task send/get checks with valid message IDs, on both HTTP bindings | Full ACTS corpus, authentication/deviation modes, other `tck-*` behaviors |

The harness accepts ITK instructions only from a locally bound HTTP server and
restricts outbound peer card URLs to loopback HTTP. Do not deploy it as an agent
or expose it through a tunnel. It advertises no authentication requirement.
Results are diagnostic: a failure may be in the Agents SDK bridge, a2a-dotnet,
the peer, or the test kit. Keep the JSON reports and agent logs when investigating.

## Prerequisites

- .NET SDK from [`global.json`](../global.json) (currently 10.0.401).
- Docker (the upstream kit's Python launcher uses Unix-only APIs and cannot run
  natively on Windows). The kit image supplies Python, uv, and .NET.
- GitHub/network access for the first peer build. No credentials are needed.

Use an a2a-itk checkout at revision
`bcce1a15fdce343e2d816ee7e7119f68147385de`. Pinning the revision keeps the
scenario schema and ACTS test IDs stable. The commands below use PowerShell; run
them at the root of a normal Git clone of this repository. A Git worktree whose
`.git` file points outside the Docker mount is not sufficient for the SDK's
versioning build task inside the container.

```powershell
$sdkRepo = (Resolve-Path .).Path
git clone https://github.com/a2aproject/a2a-itk.git ..\a2a-itk
$kitRepo = (Resolve-Path ..\a2a-itk).Path
git -C $kitRepo checkout bcce1a15fdce343e2d816ee7e7119f68147385de
$reportDir = Join-Path $sdkRepo 'itk\reports'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
dotnet build (Join-Path $sdkRepo 'itk\AgentsA2AItk.csproj') -c Release
docker build -t agents-a2a-itk $kitRepo
```

### ITK: interop traversal

```powershell
docker run --rm `
  --mount "type=bind,source=${sdkRepo},target=/app/agents/repo" `
  --mount "type=bind,source=${reportDir},target=/reports" `
  agents-a2a-itk /app/.venv/bin/python run_tests.py `
  --mount /app/agents/repo/itk `
  --scenarios /app/agents/repo/itk/scenarios.yaml `
  --output /reports/itk.json --log-dir /reports/logs
```

On the baseline used for this contribution (`Agents-for-net` `e08ed1bf`, kit
`bcce1a15`), JSON-RPC reports `PASS` and HTTP+JSON reports `FAIL`. The latter
is an actionable integration failure, not an unsupported binding: the current
HTTP+JSON route returns a JSON-RPC response envelope instead of a bare task.
If startup fails, inspect `reports/logs/agent_current.log` and verify that the
container's .NET SDK satisfies this repository's `global.json`.

### ACTS: targeted conformance probes

Run the upstream Agent Card checks first:

```powershell
docker run --rm `
  --mount "type=bind,source=${sdkRepo},target=/app/agents/repo" `
  --mount "type=bind,source=${reportDir},target=/reports" `
  agents-a2a-itk /app/.venv/bin/python run_acts.py `
  --mount /app/agents/repo/itk `
  --sdk agents-for-net --language dotnet `
  --transport jsonrpc --transport rest `
  -t CARD-DISC-001 -t CARD-DISC-002 -t CARD-DISC-003 `
  --out /reports/acts-card
```

These three checks pass on both bindings. For task lifecycle checks, use the
[focused ACTS suite](../acts/agents-sdk.acts.yaml):

```powershell
docker run --rm `
  --mount "type=bind,source=${sdkRepo},target=/app/agents/repo" `
  --mount "type=bind,source=${reportDir},target=/reports" `
  agents-a2a-itk /app/.venv/bin/python run_acts.py `
  --mount /app/agents/repo/itk `
  --sdk agents-for-net --language dotnet `
  --suite /app/agents/repo/acts/agents-sdk.acts.yaml `
  --behaviors /app/agents/repo/acts/sut-behaviors.yaml `
  --transport jsonrpc --transport rest `
  --out /reports/acts-tasks
```

The current upstream `CORE-SEND-001` and `CORE-GET-001` fixtures omit
`Message.messageId`, which this SDK requires; the focused suite adds it without
changing the operations or expected task states. ACTS also sends its synthetic
`Bearer itk-valid-token` on abstract operations. Because this harness is
anonymous, it removes only that test token before the SDK's authentication
parser sees it. This is not a production authentication pattern.

`rest` is ACTS's name for the HTTP+JSON binding. On the stated baseline, the
focused task checks pass 2/2 on JSON-RPC and pass 0/2 on HTTP+JSON for the
same response-envelope defect seen by ITK. The report records each failure.
If the agent is not reachable, ACTS exits before producing a test verdict.
Reports under `itk/reports/` are ignored by Git; retain them separately when
reporting a failure, together with the SDK and kit commit IDs. Do not interpret
this selected run as a full conformance result.

The container commands use the image's preinstalled virtual environment rather
than `uv run`. The latter tries to rebuild the kit's editable package after the
SDK checkout is mounted below `/app`, which can needlessly scan the entire SDK
checkout.

## Why this is not a required PR check

The upstream a2a-dotnet suite already owns broad protocol compliance. This
additional set is deliberately small and currently runs manually. Before
considering scheduled execution or a required PR check, collect repeatable
results, verify that failures are attributable to the Agents SDK integration,
and budget for peer-fetch and build time.
