// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace DialogSkillBot.Dialogs
{
    /// <summary>
    /// A root dialog that can route activities sent to the skill to different sub-dialogs.
    /// </summary>
    public class ActivityRouterDialog : ComponentDialog
    {
        public ActivityRouterDialog()
            : base(nameof(ActivityRouterDialog))
        {
            AddDialog(new BookingDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[] { ProcessActivityAsync }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ProcessActivityAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // A skill can send trace activities, if needed.
            await stepContext.Context.TraceActivityAsync($"{GetType().Name}.ProcessActivityAsync()", label: $"Got ActivityType: {stepContext.Context.Activity.Type}", cancellationToken: cancellationToken);

            switch (stepContext.Context.Activity.Type)
            {
                case ActivityTypes.Event:
                    return await OnEventActivityAsync(stepContext, cancellationToken);

                case ActivityTypes.Message:
                    return await OnMessageActivityAsync(stepContext, cancellationToken);

                default:
                    // We didn't get an activity type we can handle.
                    await stepContext.Context.SendActivityAsync(new MessageActivity($"Unrecognized ActivityType: \"{stepContext.Context.Activity.Type}\".", inputHint: InputHints.IgnoringInput), cancellationToken);
                    return new DialogTurnResult(DialogTurnStatus.Complete);
            }
        }

        // This method performs different tasks based on the event name.
        private async Task<DialogTurnResult> OnEventActivityAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activity = stepContext.Context.Activity;
            var eventActivity = activity as IEventActivity;
            await stepContext.Context.TraceActivityAsync($"{GetType().Name}.OnEventActivityAsync()", label: $"Name: {eventActivity?.Name}. Value: {GetObjectAsJsonString(eventActivity?.Value)}", cancellationToken: cancellationToken);

            // Resolve what to execute based on the event name.
            switch (eventActivity?.Name)
            {
                case "BookFlight":
                    return await BeginBookFlight(stepContext, cancellationToken);

                case "GetWeather":
                    return await BeginGetWeather(stepContext, cancellationToken);

                default:
                    // We didn't get an event name we can handle.
                    await stepContext.Context.SendActivityAsync(new MessageActivity($"Unrecognized EventName: \"{eventActivity?.Name}\".", inputHint: InputHints.IgnoringInput), cancellationToken);
                    return new DialogTurnResult(DialogTurnStatus.Complete);
            }
        }

        // This method just gets a message activity and runs it through LUIS. 
        private async Task<DialogTurnResult> OnMessageActivityAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activity = stepContext.Context.Activity;
            var messageActivity = activity as IMessageActivity;

            // Start a dialog if we recognize the intent.
            switch (messageActivity?.Text)
            {
                case "BookFlight":
                    return await BeginBookFlight(stepContext, cancellationToken);

                case "GetWeather":
                    return await BeginGetWeather(stepContext, cancellationToken);

                default:
                    // Catch all for unhandled intents.
                    var didntUnderstandMessageText = "Please say \"BookFlight\" or \"GetWeather\"";
                    var didntUnderstandMessage = new MessageActivity(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return new DialogTurnResult(DialogTurnStatus.Complete);
        }

        private static async Task<DialogTurnResult> BeginGetWeather(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activity = stepContext.Context.Activity;
            var eventActivity = activity as IEventActivity;
            var location = new Location();
            if (eventActivity?.Value != null)
            {
                location = ProtocolJsonSerializer.ToObject<Location>(eventActivity.Value);
            }

            // We haven't implemented the GetWeatherDialog so we just display a TODO message.
            var getWeatherMessageText = $"TODO: get weather for here (lat: {location.Latitude}, long: {location.Longitude}";
            var getWeatherMessage = new MessageActivity(getWeatherMessageText, getWeatherMessageText, InputHints.IgnoringInput);
            await stepContext.Context.SendActivityAsync(getWeatherMessage, cancellationToken);
            return new DialogTurnResult(DialogTurnStatus.Complete);
        }

        private async Task<DialogTurnResult> BeginBookFlight(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activity = stepContext.Context.Activity;
            var eventActivity = activity as IEventActivity;
            var bookingDetails = new BookingDetails();
            if (eventActivity?.Value != null)
            {
                bookingDetails = ProtocolJsonSerializer.ToObject<BookingDetails>(eventActivity.Value);
            }

            // Start the booking dialog.
            var bookingDialog = FindDialog(nameof(BookingDialog));
            return await stepContext.BeginDialogAsync(bookingDialog.Id, bookingDetails, cancellationToken);
        }

        private string GetObjectAsJsonString(object value) => value == null ? string.Empty : ProtocolJsonSerializer.ToJson(value);
    }
}
