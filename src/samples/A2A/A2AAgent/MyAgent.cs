// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent;

[Agent(name: "MyAgent", description: "Agent with A2A Sample")]
[A2AExtension]
[AgentInterface(A2AAgentTransportProtocol.JsonRpc, "/a2a")]
[AgentInterface(A2AAgentTransportProtocol.HttpJson, "/a2a")]
public partial class MyAgent : AgentApplication
{
    private const string GraphHandlerName = "graph";
    private const string GitHubHandlerName = "github";
    private readonly IGraphProfileClient _graphClient;
    private readonly IGitHubIssuesClient _gitHubIssuesClient;

    public MyAgent(AgentApplicationOptions options, IGraphProfileClient graphClient, IGitHubIssuesClient gitHubIssuesClient) : base(options)
    {
        _graphClient = graphClient;
        _gitHubIssuesClient = gitHubIssuesClient;
    }

    [A2ASkill(name: "Microsoft Graph profile", description: "Reads the delegated caller profile from Microsoft Graph.", tags: "a2a, sample, authentication, graph", examples: "-me", text: "-me", autoSigninHandlers: GraphHandlerName)]
    private async Task OnGraphAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string token = await UserAuthorization.GetTurnTokenAsync(turnContext, GraphHandlerName, cancellationToken).ConfigureAwait(false);
        GraphProfile profile = await _graphClient.GetMeAsync(token, cancellationToken).ConfigureAwait(false);
        await CompleteTaskAsync(
            turnContext,
            $"Name: {profile.DisplayName}{Environment.NewLine}Email: {profile.MailOrUserPrincipalName}",
            cancellationToken).ConfigureAwait(false);
    }

    [A2ASkill(name: "GitHub assigned issues", description: "Reads the signed-in GitHub user's open assigned issues.", tags: "a2a, sample, authentication, github, issues", examples: "-issues", text: "-issues", autoSigninHandlers: GitHubHandlerName)]
    private async Task OnGitHubIssuesAsync(IA2ATurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string token = await UserAuthorization.GetTurnTokenAsync(turnContext, GitHubHandlerName, cancellationToken).ConfigureAwait(false);
        string summary = await _gitHubIssuesClient.GetAssignedIssuesSummaryAsync(token, cancellationToken).ConfigureAwait(false);
        await CompleteTaskAsync(
            turnContext,
            summary,
            cancellationToken).ConfigureAwait(false);
    }

    private static Task CompleteTaskAsync(IA2ATurnContext turnContext, string text, CancellationToken cancellationToken)
    {
        return turnContext.SendActivityAsync(
            new Activity
            {
                Type = ActivityTypes.EndOfConversation,
                Text = text,
                Code = EndOfConversationCodes.CompletedSuccessfully,
            },
            cancellationToken: cancellationToken);
    }
}
