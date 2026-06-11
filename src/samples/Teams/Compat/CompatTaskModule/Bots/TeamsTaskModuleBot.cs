// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCards;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CompatTaskModule.Models;

namespace CompatTaskModule.Bots
{
    public class TeamsTaskModuleBot : TeamsActivityHandler
    {
        private readonly string _baseUrl;

        public TeamsTaskModuleBot(IConfiguration config)
        {
            _baseUrl = config["BaseUrl"].EndsWith("/") ? config["BaseUrl"] : config["BaseUrl"] + "/";
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var reply = MessageFactory.Attachment(GetTaskModuleHeroCardOptions());
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        protected override Task<Microsoft.Teams.Api.TaskModules.Response> OnTeamsTaskModuleFetchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.TaskModules.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            var asJobject = JsonObject.Parse(taskModuleRequest.Data.ToString());

            // Fetch the 'Data' field from the JSON object
            var value = asJobject["Data"]?.ToString();

            var taskInfo = new Microsoft.Teams.Api.TaskModules.TaskInfo();
            
            switch (value)
            {
                case TaskModuleIds.YouTube:
                    taskInfo.Url = taskInfo.FallbackUrl = _baseUrl + TaskModuleIds.YouTube;
                    SetTaskInfo(taskInfo, TaskModuleUIConstants.YouTube);
                    break;
                case TaskModuleIds.CustomForm:
                    taskInfo.Url = taskInfo.FallbackUrl = _baseUrl + TaskModuleIds.CustomForm;
                    SetTaskInfo(taskInfo, TaskModuleUIConstants.CustomForm);
                    break;
                case TaskModuleIds.AdaptiveCard:
                    taskInfo.Card = CreateAdaptiveCardAttachment();
                    SetTaskInfo(taskInfo, TaskModuleUIConstants.AdaptiveCard);
                    break;
                default:
                    break;
            }

            return Task.FromResult(taskInfo.ToTaskModuleResponse());
        }

        protected override async Task<Microsoft.Teams.Api.TaskModules.Response> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.TaskModules.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            var reply = MessageFactory.Text("OnTeamsTaskModuleSubmitAsync Value: " + JsonObject.Parse(taskModuleRequest.Data.ToString()));
            await turnContext.SendActivityAsync(reply, cancellationToken);

            return TaskModuleResponseFactory.CreateResponse("Thanks!");
        }

        private static void SetTaskInfo(Microsoft.Teams.Api.TaskModules.TaskInfo taskInfo, UISettings uIConstants)
        {
            taskInfo.Height = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(uIConstants.Height);
            taskInfo.Width = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(uIConstants.Width);
            taskInfo.Title = uIConstants.Title.ToString();
        }

        private static Attachment GetTaskModuleHeroCardOptions()
        {
            // Create a Hero Card with TaskModuleActions for each Dialog (referred to as task modules in TeamsJS v1.x)
            return new HeroCard()
            {
                Title = "Dialogs (referred to as task modules in TeamsJS v1.x) Invocation from Hero Card",
                Buttons = [.. new[]
                {
                    TaskModuleUIConstants.AdaptiveCard,
                    TaskModuleUIConstants.CustomForm,
                    TaskModuleUIConstants.YouTube
                }.Select(cardType =>
                    new TaskModuleAction(
                        cardType.ButtonTitle,
                        new CardTaskFetchValue<string>
                        {
                            Data = cardType.Id
                        }))]
            }
            .ToAttachment();
        }

        private static Attachment GetTaskModuleAdaptiveCardOptions()
        {
            // Create an Adaptive Card with an AdaptiveSubmitAction for each Dialogs (referred as task modules in TeamsJS v1.x)
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 2))
            {
                Body = new List<AdaptiveElement>()
                    {
                        new AdaptiveTextBlock(){ Text="Dialogs (referred as task modules in TeamsJS v1.x) Invocation from Adaptive Card", Weight=AdaptiveTextWeight.Bolder, Size=AdaptiveTextSize.Large}
                    },
                Actions = new[] { TaskModuleUIConstants.AdaptiveCard, TaskModuleUIConstants.CustomForm, TaskModuleUIConstants.YouTube }
                            .Select(cardType => new AdaptiveSubmitAction() { Title = cardType.ButtonTitle, Data = new AdaptiveCardTaskFetchValue<string>() { Data = cardType.Id } })
                            .ToList<AdaptiveAction>(),
            };

            return new Attachment() { ContentType = AdaptiveCard.ContentType, Content = card };
        }

        private static Microsoft.Teams.Api.Attachment CreateAdaptiveCardAttachment()
        {
            // combine path for cross platform support
            string[] paths = { ".", "Resources", "adaptiveCard.json" };
            var adaptiveCardJson = File.ReadAllText(Path.Combine(paths));

            var adaptiveCardAttachment = new Microsoft.Teams.Api.Attachment()
            {
                ContentType = Microsoft.Teams.Api.ContentType.AdaptiveCard,
                Content = (adaptiveCardJson),
            };
            return adaptiveCardAttachment;
        }
    }
}
