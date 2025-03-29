# Microsoft.Agents.Authentication.Msal

Provides the MSAL IAccessTokenProvider implementation to get tokens.

## Example configuration

```
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
        "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
        "ClientId": "00000000-0000-0000-0000-000000000000", // this is the Client ID used for the connection.
        "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  }
```
