// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Agents.Core.Models;
using System.Text.Json.Nodes;
using Microsoft.Agents.Telemetry;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Dialogs.Debugging;
using System;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    [Collection("DebugSupport.SourceMap")]
    public class RecognizerTests
    {
        [Fact]
        public async Task LogsTelemetry()
        {
            var telemetryClient = new Mock<IBotTelemetryClient>();

            var recognizer = new MyRecognizerSubclass { TelemetryClient = telemetryClient.Object };
            var adapter = new TestAdapter(TestAdapter.CreateConversation("RecognizerLogsTelemetry"));
            var activity = MessageFactory.Text("hi");
            var context = new TurnContext(adapter, activity);
            var dc = new DialogContext(new DialogSet(), context, new DialogState());

            var result = await recognizer.RecognizeAsync<RecognizerResult>(dc, activity);

            var actualTelemetryProps = (IDictionary<string, string>)telemetryClient.Invocations[0].Arguments[1];

            Assert.NotNull(result);
            Assert.Equal("hi", actualTelemetryProps["Text"]);
            Assert.Null(actualTelemetryProps["AlteredText"]);
            actualTelemetryProps.TryGetValue("TopIntent", out var intent);
            Assert.Equal("myTestIntent", intent);
            Assert.Equal("1.0", actualTelemetryProps["TopIntentScore"]);
            var hasMyTestIntent = actualTelemetryProps["Intents"].Contains("myTestIntent");
            Assert.True(hasMyTestIntent);
            Assert.Equal("{}", actualTelemetryProps["Entities"]);
            Assert.Null(actualTelemetryProps["Properties"]);

            telemetryClient.Verify(
                client => client.TrackEvent(
                    "MyRecognizerSubclassResult",
                    It.IsAny<IDictionary<string, string>>(),
                    null),
                Times.Once());
        }

        [Fact]
        public void Constructor_ShouldSetSourceMap()
        {
            var path = "path";
            var oldSourceMap = DebugSupport.SourceMap;
            DebugSupport.SourceMap = new SourceMap();

            var recognizer = new Recognizer(path, 3);
            DebugSupport.SourceMap.TryGetValue(recognizer, out var range);

            Assert.Equal(path, range.Path);
            Assert.Equal(3, range.StartPoint.LineIndex);
            Assert.Equal(4, range.EndPoint.LineIndex);
            
            DebugSupport.SourceMap = oldSourceMap;
        }

        [Fact]
        public async Task RecognizeAsync_ShouldThrowNotImplementedException()
        {
            var recognizer = new Recognizer();

            await Assert.ThrowsAsync<NotImplementedException>(() => recognizer.RecognizeAsync(null, null));
        }

        [Fact]
        public void CreateChooseIntentResult_ShouldReturnResultWithCandidates()
        {
            var candidateId = "candidateId";
            var result = new RecognizerResult
            {
                Text = "testing",
                AlteredText = null,
                Intents = new Dictionary<string, IntentScore>
                {
                    {"myTestIntent", new IntentScore { Score = 1.0, Properties = new Dictionary<string, object>() }}
                },
                Entities = [],
                Properties = new Dictionary<string, object>()
            };

            var recognizerResults = new Dictionary<string, RecognizerResult> { { candidateId, result } };
            var resultWithCandidates = MyRecognizerSubclass.CreateChooseIntentResultInternal(recognizerResults);

            Assert.Single(resultWithCandidates.Properties);
            Assert.Equal(candidateId, ((List<JsonObject>)resultWithCandidates.Properties["candidates"])[0]!["id"]!.ToString());
        }

        [Fact]
        public void CreateChooseIntentResult_ShouldReturnNoneIntentResult()
        {
            var recognizerResults = new Dictionary<string, RecognizerResult>();
            var resultWithCandidates = MyRecognizerSubclass.CreateChooseIntentResultInternal(recognizerResults);

            Assert.Single(resultWithCandidates.Intents);
            Assert.NotNull(resultWithCandidates.Intents["None"]);
        }

        [Fact]
        public async Task TrackRecognizerResult_ShouldUseCustomProperties()
        {
            var properties = new Dictionary<string, string>
            {
                { "Testing", "testing" },
            };

            var telemetryClient = new Mock<IBotTelemetryClient>();

            telemetryClient.Setup(e => e.TrackEvent(
                    It.IsAny<string>(),
                    It.Is<IDictionary<string, string>>(e => e.ContainsKey("Testing")),
                    It.IsAny<IDictionary<string, double>>()))
                .Verifiable(Times.Once);

            var recognizer = new MyRecognizerSubclass();
            var adapter = new TestAdapter(TestAdapter.CreateConversation("RecognizerLogsTelemetry"));
            var activity = MessageFactory.Text("hi");
            var context = new TurnContext(adapter, activity);
            context.Services.Set(telemetryClient.Object);
            var dc = new DialogContext(new DialogSet(), context, new DialogState());

            var result = await recognizer.RecognizeAsync(dc, activity, telemetryProperties: properties);

            Mock.Verify(telemetryClient);
        }

        /// <summary>
        /// Subclass to test <see cref="Recognizer.FillRecognizerResultTelemetryProperties(RecognizerResult, Dictionary{string,string}, DialogContext)"/> functionality.
        /// </summary>
        private class MyRecognizerSubclass : Recognizer
        {
            public override async Task<RecognizerResult> RecognizeAsync(DialogContext dialogContext, IActivity activity, CancellationToken cancellationToken = default, Dictionary<string, string> telemetryProperties = null, Dictionary<string, double> telemetryMetrics = null)
            {
                var text = activity.Text ?? string.Empty;

                var recognizerResult = await Task.FromResult(new RecognizerResult
                {
                    Text = text,
                    AlteredText = null,
                    Intents = new Dictionary<string, IntentScore>
                    {
                        {
                            "myTestIntent", new IntentScore
                            {
                                Score = 1.0,
                                Properties = new Dictionary<string, object>()
                            }
                        }
                    },
                    Entities = new JsonObject(),
                    Properties = new Dictionary<string, object>()
                });

                TrackRecognizerResult(dialogContext, $"{nameof(MyRecognizerSubclass)}Result", FillRecognizerResultTelemetryProperties(recognizerResult, telemetryProperties, dialogContext), telemetryMetrics);

                return recognizerResult;
            }

            public static RecognizerResult CreateChooseIntentResultInternal(Dictionary<string, RecognizerResult> recognizerResults)
            {
                return CreateChooseIntentResult(recognizerResults);
            }
        }

    }
}
