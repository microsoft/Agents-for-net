// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Channel definitions.
    /// The various "support" methods come from:  https://learn.microsoft.com/en-us/azure/bot-service/bot-service-channels-reference?view=azure-bot-service-4.0
    /// </summary>
    public static class Channels
    {
        /// <summary>
        /// Alexa channel.
        /// </summary>
        public const string Alexa = "alexa";

        /// <summary>
        /// Console channel.
        /// </summary>
        public const string Console = "console";

        /// <summary>
        /// Cortana channel.
        /// </summary>
        public const string Cortana = "cortana";

        /// <summary>
        /// Direct Line channel.
        /// </summary>
        public const string Directline = "directline";

        /// <summary>
        /// Direct Line Speech channel.
        /// </summary>
        public const string DirectlineSpeech = "directlinespeech";

        /// <summary>
        /// Email channel.
        /// </summary>
        public const string Email = "email";

        /// <summary>
        /// Emulator channel.
        /// </summary>
        public const string Emulator = "emulator";

        /// <summary>
        /// Facebook channel.
        /// </summary>
        public const string Facebook = "facebook";

        /// <summary>
        /// Group Me channel.
        /// </summary>
        public const string Groupme = "groupme";

        /// <summary>
        /// Kik channel.
        /// </summary>
        public const string Kik = "kik";

        /// <summary>
        /// Line channel.
        /// </summary>
        public const string Line = "line";

        /// <summary>
        /// MS Teams channel.
        /// </summary>
        public const string Msteams = "msteams";

        /// <summary>
        /// Skype channel.
        /// </summary>
        public const string Skype = "skype";

        /// <summary>
        /// Skype for Business channel.
        /// </summary>
        public const string Skypeforbusiness = "skypeforbusiness";

        /// <summary>
        /// Slack channel.
        /// </summary>
        public const string Slack = "slack";

        /// <summary>
        /// SMS (Twilio) channel.
        /// </summary>
        public const string Sms = "sms";

        /// <summary>
        /// Telegram channel.
        /// </summary>
        public const string Telegram = "telegram";

        /// <summary>
        /// WebChat channel.
        /// </summary>
        public const string Webchat = "webchat";

        /// <summary>
        /// Test channel.
        /// </summary>
        public const string Test = "test";

        /// <summary>
        /// Twilio channel.
        /// </summary>
        public const string Twilio = "twilio-sms";

        /// <summary>
        /// Telephony channel.
        /// </summary>
        public const string Telephony = "telephony";

        /// <summary>
        /// Omni channel.
        /// </summary>
        public const string Omni = "omnichannel";

        /// <summary>
        /// Outlook channel.
        /// </summary>
        public const string Outlook = "outlook";

        /// <summary>
        /// M365 channel.
        /// </summary>
        public const string M365 = "m365extensions";

        /// <summary>
        /// M365 Copilot Teams Subchannel
        /// </summary>
        public const string M365CopilotSubChannel = "COPILOT";
        public const string M365Copilot = $"{Msteams}:{M365CopilotSubChannel}";

        /// <summary>
        /// Determine if a number of Suggested Actions are supported by a Channel.
        /// </summary>
        /// <param name="channelId">The Channel to check the if Suggested Actions are supported in.</param>
        /// <param name="buttonCnt">(Optional) The number of Suggested Actions to check for the Channel.</param>
        /// <returns>True if the Channel supports the buttonCnt total Suggested Actions, False if the Channel does not support that number of Suggested Actions.</returns>
        public static bool SupportsSuggestedActions(ChannelId channelId, int buttonCnt = 100)
        {
            return SupportsSuggestedActions(channelId, buttonCnt, null);
        }

        /// <summary>
        /// Determine if a number of Suggested Actions are supported by a Channel.
        /// </summary>
        /// <param name="channelId">The Channel to check the if Suggested Actions are supported in.</param>
        /// <param name="buttonCnt">(Optional) The number of Suggested Actions to check for the Channel.</param>
        /// <param name="conversationType">(Optional) The type of the conversation.</param>
        /// <returns>True if the Channel supports the buttonCnt total Suggested Actions, False if the Channel does not support that number of Suggested Actions.</returns>
        public static bool SupportsSuggestedActions(ChannelId channelId, int buttonCnt = 100, string conversationType = default)
        {
            switch (channelId.Channel)
            {
                // https://developers.facebook.com/docs/messenger-platform/send-messages/quick-replies
                case Facebook:
                case Skype:
                    return buttonCnt <= 10;

                // https://developers.line.biz/en/reference/messaging-api/#items-object
                case Line:
                    return buttonCnt <= 13;

                // https://dev.kik.com/#/docs/messaging#text-response-object
                case Kik:
                    return buttonCnt <= 20;

                case Telegram:
                case Emulator:
                case Directline:
                case DirectlineSpeech:
                case Webchat:
                    return buttonCnt <= 100;

                // https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/conversation-messages?tabs=dotnet1%2Cdotnet2%2Cdotnet3%2Cdotnet4%2Cdotnet5%2Cdotnet#send-suggested-actions
                case M365Copilot:
                case Msteams:
                    if (conversationType == "personal")
                    {
                        return buttonCnt <= 3;
                    }

                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determine if a number of Card Actions are supported by a Channel.
        /// </summary>
        /// <param name="channelId">The Channel to check if the Card Actions are supported in.</param>
        /// <param name="buttonCnt">(Optional) The number of Card Actions to check for the Channel.</param>
        /// <returns>True if the Channel supports the buttonCnt total Card Actions, False if the Channel does not support that number of Card Actions.</returns>
        public static bool SupportsCardActions(ChannelId channelId, int buttonCnt = 100)
        {
            switch (channelId.Channel)
            {
                case Facebook:
                case Skype:
                    return buttonCnt <= 3;

                case Msteams:
                    return buttonCnt <= 50;

                case Line:
                    return buttonCnt <= 99;

                case Slack:
                case Telegram:
                case Emulator:
                case Directline:
                case DirectlineSpeech:
                case Webchat:
                case Cortana:
                    return buttonCnt <= 100;

                default:
                    return false;
            }
        }

        public static bool SupportsVideoCard(string channelId)
        {
            switch (channelId)
            {
                case Alexa:
                case Msteams:
                case Twilio:
                    return false;

                default:
                    return true;
            }
        }

        public static bool SupportsReceiptCard(string channelId)
        {
            switch (channelId)
            {
                case Alexa:
                case Groupme:
                case Msteams:
                case Twilio:
                    return false;

                default:
                    return true;
            }
        }

        public static bool SupportsThumbnailCard(string channelId)
        {
            switch (channelId)
            {
                case Alexa:
                    return false;

                // Text only
                case Groupme:
                case Line:
                case Slack:
                case Twilio:
                    return false;

                default:
                    return true;
            }
        }

        public static bool SupportsAudioCard(string channelId)
        {
            switch (channelId)
            {
                case Alexa:
                case Msteams:
                case Twilio:
                    return false;

                // Text only
                case Email:
                case Groupme:
                case Line:
                case Slack:
                case Telegram:
                    return false;

                default:
                    return true;
            }
        }

        public static bool SupportsAnimationCard(string channelId)
        {
            switch (channelId)
            {
                case Alexa:
                case Msteams:
                    return false;

                // Text only
                case Email:
                case Groupme:
                case Twilio:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Determine if a Channel has a Message Feed.
        /// </summary>
        /// <param name="channelId">The Channel to check for Message Feed.</param>
        /// <returns>True if the Channel has a Message Feed, False if it does not.</returns>
        public static bool HasMessageFeed(ChannelId channelId)
        {
            switch (channelId.Channel)
            {
                case Cortana:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Maximum length allowed for Action Titles.
        /// </summary>
        /// <param name="channelId">The Channel to determine Maximum Action Title Length.</param>
        /// <returns>The total number of characters allowed for an Action Title on a specific Channel.</returns>
        public static int MaxActionTitleLength(ChannelId channelId) => 20;

        /// <summary>
        /// Returns channel support for CreateConversation.
        /// </summary>
        /// <param name="channelId"></param>
        public static bool SupportsCreateConversation(ChannelId channelId)
        {
            switch (channelId.Channel)
            {
                case Webchat:
                case Directline:
                case Alexa:
                    return false;

                case Email:
                case Facebook:
                case Groupme:
                case Kik:
                case Line:
                case Msteams:
                case Slack:
                case Sms:
                case Telegram:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns channel support for UpdateActivity.
        /// </summary>
        /// <param name="channelId"></param>
        public static bool SupportsUpdateActivity(ChannelId channelId)
        {
            switch (channelId.Channel)
            {
                case Msteams:
                    return true;

                default: 
                    return false;
            }
        }

        /// <summary>
        /// Returns channel support for DeleteActivity.
        /// </summary>
        /// <param name="channelId"></param>
        public static bool SupportsDeleteActivity(ChannelId channelId)
        {
            switch (channelId.Channel)
            {
                case Alexa:
                case Directline:
                case Email:
                case Facebook:
                case Groupme:
                case Kik:
                case Line:
                case Sms:
                case Webchat:
                    return false;

                case Msteams:
                case Slack:
                case Telegram:
                    return true;

                default:
                    return false;
            }
        }
    }
}
