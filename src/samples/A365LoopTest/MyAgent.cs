// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentNotification;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;


namespace A365LoopTest;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options, IConnections connections) : base(options)
    {
        // Register the agent Notification Handler for all notifications from the agent.  The handler can filter for specific notification types if needed, but in this example we will handle them all in the same place.
        this.OnAgenticEmailNotification(HandelAgentNotifications, autoSignInHandlers: ["agentic"]);
        this.OnAgenticExcelNotification(HandelAgentNotifications, autoSignInHandlers: ["agentic"]);
        this.OnAgenticPowerPointNotification(HandelAgentNotifications, autoSignInHandlers: ["agentic"]);
        this.OnAgenticWordNotification(HandelAgentNotifications, autoSignInHandlers: ["agentic"]);

        // Handles A365 Life cycle events. 
        this.OnLifecycleNotification(HandleLifeCycleNotification, autoSignInHandlers: ["agentic"]);
    }

    private async Task HandleLifeCycleNotification(ITurnContext context, ITurnState turnState, AgentNotificationActivity agentNotificationActivity, CancellationToken cancellationToken)
    {
        // Get user token for agentic user. 
        var aauToken = await UserAuthorization.GetTurnTokenAsync(context, "agentic", cancellationToken);

        // Log what type of lifestyle notification was received. 
        System.Diagnostics.Trace.WriteLine($"A365 LIFECYCLE NOTIFICATION TYPE >>> Type: {agentNotificationActivity.NotificationType}, Value: {agentNotificationActivity.ValueType}");
    }

    private async Task HandelAgentNotifications(ITurnContext context, ITurnState turnState, AgentNotificationActivity agentNotificationActivity, CancellationToken cancellationToken)
    {
        // Get user token for agentic user. 
        var aauToken = await UserAuthorization.GetTurnTokenAsync(context, "agentic", cancellationToken);

        // Log what type of notification was received. 
        System.Diagnostics.Trace.WriteLine($"A365 NOTIFICATION TYPE >>> Type: {agentNotificationActivity.NotificationType}");
        IActivity? response = null; 
        switch (agentNotificationActivity.NotificationType)
        {
            case NotificationTypeEnum.Unknown:
                System.Diagnostics.Trace.WriteLine($"A365 {NotificationTypeEnum.Unknown} Event Not handled");
                break;
            case NotificationTypeEnum.WpxComment:
                response = MessageFactory.CreateMessageActivity($"Comment Received By loop Test tool at {DateTime.Now.ToShortTimeString()}");
                await context.SendActivityAsync(response);
                break;
            case NotificationTypeEnum.EmailNotification:
                response =  context.Activity.CreateEmailResponseActivity($"Email Received By loop Test tool at {DateTime.Now.ToShortTimeString()}");
                await context.SendActivityAsync(response); 
                break;
            case NotificationTypeEnum.FederatedKnowledgeServiceNotification:
                System.Diagnostics.Trace.WriteLine($"A365 {NotificationTypeEnum.FederatedKnowledgeServiceNotification} Event Not handled");
                break;
            case NotificationTypeEnum.AgentLifecycleNotification:
                System.Diagnostics.Trace.WriteLine($"A365 {NotificationTypeEnum.AgentLifecycleNotification} Event Not handled"); 
                break;
            default:
                System.Diagnostics.Trace.WriteLine($"A365 {context.Activity.Type} Event Fell Through");
                break;
        }
    }
}
