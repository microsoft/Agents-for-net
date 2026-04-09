// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Well-known service endpoint URLs for sending proactive messages to Microsoft Teams.
/// Use these only if the incoming request serviceUrl is unavailable; once a serviceUrl has been returned from a prior conversation, cache and use that value instead.
/// </summary>
public static class TeamsProactiveServiceEndpoints
{
    /// <summary>
    /// Service endpoint for the public global Teams environment.
    /// </summary>
    public static readonly string publicGlobal = "https://smba.trafficmanager.net/teams/";

    /// <summary>
    /// Service endpoint for the GCC (Government Community Cloud) Teams environment.
    /// </summary>
    public static readonly string gcc = "https://smba.infra.gcc.teams.microsoft.com/teams";

    /// <summary>
    /// Service endpoint for the GCC High Teams environment.
    /// </summary>
    public static readonly string gccHigh = "https://smba.infra.gov.teams.microsoft.us/teams";

    /// <summary>
    /// Service endpoint for the DoD (Department of Defense) Teams environment.
    /// </summary>
    public static readonly string dod = "https://smba.infra.dod.teams.microsoft.us/teams";
}
