// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using SidecarAuth;
using Microsoft.Agents.Authentication.EntraAuthSidecar.HealthChecks;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()

    // Add the AgentApplication, which contains the logic for responding to user messages.
    .AddAgent<SidecarAgent>()

    // Report whether the Entra ID sidecar is reachable at "/health" (mapped below).
    .AddSidecarHealthCheck()

    // Optionally probe the sidecar once at startup. With failOnUnreachable:false (the default) an
    // unreachable sidecar logs a warning and startup continues; set true to fail fast instead.
    .AddSidecarStartupProbe(failOnUnreachable: false)

    // Add AspNet token validation for Azure Bot Service and Entra.  Authentication is configured in
    // the appsettings.json "TokenValidation" section.  Inbound bearer validation uses OIDC well-known
    // metadata (issuer + JWKS signing keys) only; it does not use the Entra sidecar or MSAL (those are
    // for outbound token acquisition).  Authorization is enabled in all environments except Development,
    // and MapDefaultAgentEndpoints requires auth on agent endpoints accordingly.
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization.
app.UseAgents();

// Map the default agent endpoints: GET "/" and the agent message endpoints.
app.MapDefaultAgentEndpoints();

// Sidecar reachability health endpoint.
app.MapHealthChecks("/health");

app.Run();
