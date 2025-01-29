// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards.Templating;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.BotBuilder.Teams;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Teams.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TagMentionBot.Dialogs
{
    public class MainDialog : LogoutDialog
    {
        protected readonly ILogger _logger;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            _logger = logger;

            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please Sign In",
                    Title = "Sign In",
                    Timeout = 300000, // User has 5 minutes to login (1000 * 60 * 5)
                    EndOnInvalidMessage = true
                }));

            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                MentionTagAsync
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        // Method to invoke auth flow.
        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("PromptStepAsync() called.");
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        // Sends tag mention adaptive card.
        private async Task<ResourceResponse> TagMentionAdaptiveCard(WaterfallStepContext stepContext, string tagName, string tagId, CancellationToken cancellationToken)
        {
            string[] path = { ".", "Resources", "UserMentionCardTemplate.json" };
            var adaptiveCardJson = System.IO.File.ReadAllText(Path.Combine(path));

            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var memberData = new
            {
                tagId = tagId,
                tagName = tagName
            };

            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = template.Expand(memberData)
            };

            return await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(adaptiveCardAttachment), cancellationToken);
        }

        // Method to invoke Tag mention functionality flow.
        private async Task<DialogTurnResult> MentionTagAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (stepContext.Context.Activity.Conversation.ConversationType == "personal" && tokenResponse?.Token != null)
            {
                await stepContext.Context.SendActivityAsync(
                    "You have successfully logged in. Please install the app in the team scope to use the Tag mention functionality.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                // Get the token from the previous step. Note that we could also have gotten the
                // token directly from the prompt itself. There is an example of this in the next method.
                bool tagExists = false;
                if (tokenResponse?.Token != null)
                {
                    try
                    {
                        stepContext.Context.Activity.RemoveRecipientMention();
                        if (stepContext.Context.Activity.Text.Trim().Contains("<at>", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var tagName = stepContext.Context.Activity.Text.Replace("<at>", string.Empty).Replace("</at>", string.Empty).Trim();
                            var mentionedEntity = stepContext.Context.Activity.GetMentions().FirstOrDefault(m => string.Equals(m.Text, stepContext.Context.Activity.Text, StringComparison.OrdinalIgnoreCase));
                            if (mentionedEntity != null)
                            {
                                var tagID = mentionedEntity.Mentioned.Id;

                                await TagMentionAdaptiveCard(stepContext, tagName, tagID, cancellationToken);
                            }
                        }
                        else if (!string.IsNullOrEmpty(stepContext.Context.Activity.Text))
                        {
                            SimpleGraphClient client = null;
                            TeamDetails teamDetails = null;

                            try
                            {
                                // Pull in the data from the Microsoft Graph.
                                client = new SimpleGraphClient(tokenResponse.Token); 
                                teamDetails = await TeamsInfo.GetTeamDetailsAsync(stepContext.Context, stepContext.Context.Activity.TeamsGetTeamInfo().Id, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                await stepContext.Context.SendActivityAsync(
                                    "You don't have Graph API permissions to fetch tag's information. Please use this command to mention a tag: \"`@<Bot-name>  @<your-tag>`\" to experience tag mention using bot.",
                                    cancellationToken: cancellationToken);
                            }

                            var result = await client.GetTag(teamDetails.AadGroupId);
                            foreach (var tagDetails in result.CurrentPage)
                            {
                                if (tagDetails.DisplayName.Equals(stepContext.Context.Activity.Text.Trim(), StringComparison.CurrentCultureIgnoreCase))
                                {
                                    tagExists = true;
                                    await TagMentionAdaptiveCard(stepContext, tagDetails.DisplayName, tagDetails.Id, cancellationToken);
                                    break;
                                }
                            }

                            if (!tagExists)
                            {
                                await stepContext.Context.SendActivityAsync(
                                    "Provided tag name is not available in this team. Please try with another tag name or create a new tag.",
                                    cancellationToken: cancellationToken);
                            }
                        }
                        else
                        {
                            await stepContext.Context.SendActivityAsync(
                                "Please provide a tag name while mentioning the bot as `@<Bot-name> <your-tag-name>` or mention a tag as `@<Bot-name> @<your-tag>`",
                                cancellationToken: cancellationToken);
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while processing your request.");
                    }
                }
                else
                {
                    _logger.LogInformation("Response token is null or empty.");
                }
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}