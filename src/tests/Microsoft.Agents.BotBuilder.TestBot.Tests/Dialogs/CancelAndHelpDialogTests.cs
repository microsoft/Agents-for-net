// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.BotBuilder.TestBot.Shared.Dialogs;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Testing.XUnit;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.BotBuilderSamples.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.BotBuilderSamples.Tests.Dialogs
{
    public class CancelAndHelpDialogTests : BotTestBase
    {
        private readonly XUnitDialogTestLogger[] _middlewares;

        public CancelAndHelpDialogTests(ITestOutputHelper output)
            : base(output)
        {
            _middlewares = new[] { new XUnitDialogTestLogger(output) };
        }

        [Theory]
        [InlineData("hi", "Hi there", "cancel")]
        [InlineData("hi", "Hi there", "quit")]
        public async Task ShouldBeAbleToCancel(string utterance, string response, string cancelUtterance)
        {
            var sut = new TestCancelAndHelpDialog();
            var testClient = new DialogTestClient(Channels.Test, sut, middlewares: _middlewares);

            var reply = await testClient.SendActivityAsync<Activity>(utterance);
            Assert.Equal(response, reply.Text);
            Assert.Equal(DialogTurnStatus.Waiting, testClient.DialogTurnResult.Status);

            reply = await testClient.SendActivityAsync<Activity>(cancelUtterance);
            Assert.Equal("Cancelling", reply.Text);
            Assert.Equal(DialogTurnStatus.Complete, testClient.DialogTurnResult.Status);
        }

        [Theory]
        [InlineData("hi", "Hi there", "help")]
        [InlineData("hi", "Hi there", "?")]
        public async Task ShouldBeAbleToGetHelp(string utterance, string response, string cancelUtterance)
        {
            var sut = new TestCancelAndHelpDialog();
            var testClient = new DialogTestClient(Channels.Test, sut, middlewares: _middlewares);

            var reply = await testClient.SendActivityAsync<Activity>(utterance);
            Assert.Equal(response, reply.Text);
            Assert.Equal(DialogTurnStatus.Waiting, testClient.DialogTurnResult.Status);

            reply = await testClient.SendActivityAsync<Activity>(cancelUtterance);
            Assert.Equal("Show Help...", reply.Text);
            Assert.Equal(DialogTurnStatus.Waiting, testClient.DialogTurnResult.Status);
        }

        /// <summary>
        /// A concrete instance of <see cref="CancelAndHelpDialog"/> for testing.
        /// </summary>
        private class TestCancelAndHelpDialog : CancelAndHelpDialog
        {
            public TestCancelAndHelpDialog()
                : base(nameof(TestCancelAndHelpDialog))
            {
                AddDialog(new TextPrompt(nameof(TextPrompt)));
                var steps = new WaterfallStep[]
                {
                    PromptStep,
                    FinalStep,
                };
                AddDialog(new WaterfallDialog("testWaterfall", steps));
                InitialDialogId = "testWaterfall";
            }

            private async Task<DialogTurnResult> PromptStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Hi there") }, cancellationToken);
            }

            private Task<DialogTurnResult> FinalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
