---
applyTo:
  - "**/UserAuth/**"
  - "**/Authentication/**"
  - "**/OAuth*"
  - "**/SignIn*"
  - "**/TokenService*"
  - "**/UserAuthorization*"
---

# OAuth & User Sign-In Flow Context

When working on OAuth, token exchange, or user sign-in code, reference these sequence diagrams:
- `docs/teams-sso-sequence-diagram.md` — High-level Teams SSO scenarios (SignedIn, No Consent, Consent Required, Exchange Failure)
- `docs/oauth-internal-sequence-diagram.md` — Detailed internal flow through UserAuthorization → UserAuthorizationDispatcher → AzureBotUserAuthorization → OAuthFlow → IUserTokenClient

## Key Design Points

- Sign-in is a **multi-turn operation** — flow state is stored between turns and deleted on completion or failure.
- The Token Service Client **caches successful tokens** from `GetTokenOrSignInResource`.
- If **OBO** is configured, OBO is performed on the token returned by Token Service prior to setting the Turn Token.
- A **Continuation Activity** is stored when sign-in starts and replayed proactively after successful token acquisition.
- **ConsentRequired** (412) triggers Teams to prompt the user, followed by a `signin/verifyState` invoke with a magic code.
- Non-consent exchange failures return **400** — Teams will NOT retry.

## Related Source Files

| Component | Path |
|-----------|------|
| UserAuthorization (App-level) | `src/libraries/Builder/Microsoft.Agents.Builder/App/UserAuth/UserAuthorization.cs` |
| UserAuthorizationDispatcher | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/UserAuthorizationDispatcher.cs` |
| AzureBotUserAuthorization | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/AzureBotUserAuthorization.cs` |
| OAuthFlow | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/OAuthFlow.cs` |
| UserTokenClientWrapper | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/UserTokenClientWrapper.cs` |
| IUserTokenClient | `src/libraries/Client/Microsoft.Agents.Connector/IUserTokenClient.cs` |
| OBOExchange | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/OBOExchange.cs` |
| Authentication.Msal | `src/libraries/Authentication/Authentication.Msal/` |
| High-level diagrams | `docs/teams-sso-sequence-diagram.md` |
| Detailed internal diagrams | `docs/oauth-internal-sequence-diagram.md` |
