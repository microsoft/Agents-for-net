// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Builder.App;

namespace Cards
{
    internal static class CardCommands
    {
        static IList<CardCommand> _cardCommands = _cardCommands =
                [
                    new CardCommand("static_submit", SendStaticSubmitCardAsync),
                    new CardCommand("dynamic_search", SendDynamicSearchCardAsync),
                    new CardCommand("action_execute", SendActionExecuteCardAsync),
                    new CardCommand("hero", SendHeroCardAsync),
                    new CardCommand("thumbnail", SendThumbnailCardAsync),
                    new CardCommand("audio", SendAudioCardAsync),
                    new CardCommand("video", SendVideoCardAsync),
                    new CardCommand("animation", SendAnimationCardAsync),
                    new CardCommand("receipt", SendReceiptCardAsync)
                ];

        public static async Task SendCardCommandsAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var commandCard = new HeroCard
            {
                Title = "Types of cards",
                Buttons = [.. _cardCommands.Select(c => new CardAction() { Title = c.Name, Type = ActionTypes.ImBack, Value = c.Name.ToLowerInvariant() })],
            };

            var activity = new Activity() { Type = ActivityTypes.Message, Attachments = [commandCard.ToAttachment()] };
            await turnContext.SendActivityAsync(activity, cancellationToken);
        }

        public static async Task<bool> OnCardCommandsAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var cardCommand = _cardCommands.Where(c => c.Name == turnContext.Activity.Text).FirstOrDefault();
            if (cardCommand == null)
            {
                await SendCardCommandsAsync(turnContext, turnState, cancellationToken);
                return false;
            }

            if (cardCommand.Name.Equals("dynamic_search") && !turnContext.Activity.ChannelId.Equals(Channels.Msteams))
            {
                await turnContext.SendActivityAsync("Only Teams channels support `dynamic_search`", cancellationToken: cancellationToken);
                return true;
            }

            if (cardCommand.Name.Equals("action_execute") && !turnContext.Activity.ChannelId.Equals(Channels.Msteams))
            {
                await turnContext.SendActivityAsync("Only Teams channels support `action_execute`", cancellationToken: cancellationToken);
                return true;
            }

            if (cardCommand.Name.Equals("audio") && !Channels.SupportsAudioCard(turnContext.Activity.ChannelId))
            {
                await turnContext.SendActivityAsync($"The channel '{turnContext.Activity.ChannelId}' does not support audio cards.", cancellationToken: cancellationToken);
                return true;
            }

            if (cardCommand.Name.Equals("video") && !Channels.SupportsVideoCard(turnContext.Activity.ChannelId))
            {
                await turnContext.SendActivityAsync($"The channel '{turnContext.Activity.ChannelId}' does not support video cards.", cancellationToken: cancellationToken);
                return true;
            }

            if (cardCommand.Name.Equals("thumbnail") && !Channels.SupportsThumbnailCard(turnContext.Activity.ChannelId))
            {
                await turnContext.SendActivityAsync($"The channel '{turnContext.Activity.ChannelId}' does not support thumbnail cards.", cancellationToken: cancellationToken);
                return true;
            }

            if (cardCommand.Name.Equals("animation") && !Channels.SupportsAnimationCard(turnContext.Activity.ChannelId))
            {
                await turnContext.SendActivityAsync($"The channel '{turnContext.Activity.ChannelId}' does not support animation cards.", cancellationToken: cancellationToken);
                return true;
            }

            if (cardCommand.Name.Equals("receipt") && !Channels.SupportsReceiptCard(turnContext.Activity.ChannelId))
            {
                await turnContext.SendActivityAsync($"The channel '{turnContext.Activity.ChannelId}' does not support receipt cards.", cancellationToken: cancellationToken);
                return true;
            }

            await cardCommand.CardHandler(turnContext, turnState, cancellationToken);
            return true;
        }

        // This will send an Adaptive Care where "StaticSubmit" is sent when the Submit button is clicked.
        public static async Task SendStaticSubmitCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = CreateAdaptiveCardAttachment(Path.Combine(".", "Resources", "StaticSearchCard.json"));
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        // This will send an Adaptive Care where "DynamicSubmit" is sent when the Submit button is clicked.  It also supports the
        // Teams "Search" functionality, which gets send with the value of "nugetpackages" to the AgentApplication.AdaptiveCards.OnSearch 
        // handler.
        public static async Task SendDynamicSearchCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = CreateAdaptiveCardAttachment(Path.Combine(".", "Resources", "DynamicSearchCard.json"));
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static async Task SendActionExecuteCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = CreateAdaptiveCardAttachment(Path.Combine(".", "Resources", "ActionExecuteWithRefresh.json"));
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        private static Attachment CreateAdaptiveCardAttachment(string filePath)
        {
            var adaptiveCardJson = File.ReadAllText(filePath);
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = adaptiveCardJson
            };
            return adaptiveCardAttachment;
        }

        public static async Task SendHeroCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = GetHeroCard().ToAttachment();
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static HeroCard GetHeroCard()
        {
            var heroCard = new HeroCard
            {
                Title = "Hero Card",
                Text = "Microsoft 365 Agents SDK provides an integrated environment that is purpose-built for agent development.",
                Images = [
                    new CardImage("https://github.com/microsoft/Agents-for-net/blob/main/src/images/agent.png?raw=true") 
                ],
                Buttons = [ 
                    new CardAction(ActionTypes.OpenUrl, "Agents SDK", value: "https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/"),
                    new CardAction(ActionTypes.OpenUrl, "Agents SDK API", value: "https://learn.microsoft.com/en-us/dotnet/api/?view=m365-agents-sdk&preserve-view=true/")
                ],
            }; 

            return heroCard;
        }

        public static async Task SendThumbnailCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = GetThumbnailCard().ToAttachment();
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static ThumbnailCard GetThumbnailCard()
        {
            var thumbnailCard = new ThumbnailCard
            {
                Title = "Thumbnail Card",
                Text = "Microsoft 365 Agents SDK provides an integrated environment that is purpose-built for agent development.",
                Images = [
                    new CardImage("https://github.com/microsoft/Agents-for-net/blob/main/src/images/agent.png?raw=true")
                ],
                Buttons = [
                    new CardAction(ActionTypes.OpenUrl, "Agents SDK", value: "https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/"),
                ],
            };

            return thumbnailCard;
        }

        public static async Task SendReceiptCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = GetReceiptCard().ToAttachment();
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static ReceiptCard GetReceiptCard()
        {
            var receiptCard = new ReceiptCard
            {
                Title = "John Doe",
                Facts = [new Fact("Order Number", "1234"), new Fact("Payment Method", "VISA 5555-****")],
                Items =
                [
                    new ReceiptItem(
                        "Data Transfer",
                        price: "$ 38.45",
                        quantity: "368",
                        image: new CardImage(url: "https://github.com/amido/azure-vector-icons/raw/master/renders/traffic-manager.png")),
                    new ReceiptItem(
                        "App Service",
                        price: "$ 45.00",
                        quantity: "720",
                        image: new CardImage(url: "https://github.com/amido/azure-vector-icons/raw/master/renders/cloud-service.png")),
                ],
                Tax = "$ 7.50",
                Total = "$ 90.95",
                Buttons =
                [
                    new CardAction(
                        ActionTypes.OpenUrl,
                        "More information",
                        "https://account.windowsazure.com/content/6.10.1.38-.8225.160809-1618/aux-pre/images/offer-icon-freetrial.png",
                        value: "https://azure.microsoft.com/en-us/pricing/"),
                ],
            };

            return receiptCard;
        }

        public static async Task SendAnimationCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = GetAnimationCard().ToAttachment();
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static AnimationCard GetAnimationCard()
        {
            var animationCard = new AnimationCard
            {
                Title = "Animation Card",
                Media =
                [
                    new MediaUrl()
                    {
                        Url = "http://i.giphy.com/Ki55RUbOV5njy.gif",
                    },
                ],
                Aspect = "4:3"
            };

            return animationCard;
        }

        public static async Task SendVideoCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = GetVideoCard().ToAttachment();
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static VideoCard GetVideoCard()
        {
            var videoCard = new VideoCard
            {
                Title = "Big Buck Bunny",
                Subtitle = "by the Blender Institute",
                Text = "Big Buck Bunny (code-named Peach) is a short computer-animated comedy film by the Blender Institute," +
                       " part of the Blender Foundation. Like the foundation's previous film Elephants Dream," +
                       " the film was made using Blender, a free software application for animation made by the same foundation." +
                       " It was released as an open-source film under Creative Commons License Attribution 3.0.",
                Aspect = "4:3",
                Image = new ThumbnailUrl
                {
                    Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c5/Big_buck_bunny_poster_big.jpg/220px-Big_buck_bunny_poster_big.jpg",
                },
                Media =
                [
                    new MediaUrl()
                    {
                        Url = "http://download.blender.org/peach/bigbuckbunny_movies/BigBuckBunny_320x180.mp4",
                    },
                ],
                Buttons =
                [
                    new CardAction()
                    {
                        Title = "Learn More",
                        Type = ActionTypes.OpenUrl,
                        Value = "https://peach.blender.org/",
                    },
                ],
            };

            return videoCard;
        }

        public static async Task SendAudioCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = GetAudioCard().ToAttachment();
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        public static AudioCard GetAudioCard()
        {
            var audioCard = new AudioCard
            {
                Title = "I am your father",
                Subtitle = "Star Wars: Episode V - The Empire Strikes Back",
                Text = "The Empire Strikes Back (also known as Star Wars: Episode V – The Empire Strikes Back)" +
                       " is a 1980 American epic space opera film directed by Irvin Kershner. Leigh Brackett and" +
                       " Lawrence Kasdan wrote the screenplay, with George Lucas writing the film's story and serving" +
                       " as executive producer. The second installment in the original Star Wars trilogy, it was produced" +
                       " by Gary Kurtz for Lucasfilm Ltd. and stars Mark Hamill, Harrison Ford, Carrie Fisher, Billy Dee Williams," +
                       " Anthony Daniels, David Prowse, Kenny Baker, Peter Mayhew and Frank Oz.",
                Image = new ThumbnailUrl
                {
                    Url = "https://upload.wikimedia.org/wikipedia/en/3/3c/SW_-_Empire_Strikes_Back.jpg",
                },
                Media =
                [
                    new MediaUrl()
                    {
                        Url = "https://www.mediacollege.com/downloads/sound-effects/star-wars/darthvader/darthvader_yourfather.wav",
                    },
                ],
                Buttons =
                [
                    new CardAction()
                    {
                        Title = "Read More",
                        Type = ActionTypes.OpenUrl,
                        Value = "https://en.wikipedia.org/wiki/The_Empire_Strikes_Back",
                    },
                ],
            };

            return audioCard;
        }
    }

    class CardCommand(string name, RouteHandler routeHandler)
    {
        public string Name { get; set; } = name;
        public RouteHandler CardHandler { get; set; } = routeHandler;
    }
}
