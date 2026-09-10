# A2AClient

`A2AClient` contains the A2A sample client's authentication core.

Current Task 2 scope adds:

- authentication mode selection (`none`, `delegated`, `app`)
- MSAL-backed delegated and application token acquisition
- an HTTP handler that attaches bearer tokens to outbound A2A requests

The interactive console conversation loop is intentionally deferred to Task 3.
