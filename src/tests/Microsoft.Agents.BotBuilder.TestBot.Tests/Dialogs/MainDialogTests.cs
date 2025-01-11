// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.BotBuilder.TestBot.Shared;
using Microsoft.Agents.BotBuilder.TestBot.Shared.Dialogs;
using Microsoft.Agents.BotBuilder.TestBot.Shared.Services;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Testing.XUnit;
using Microsoft.Agents.Core.Models;
using Microsoft.BotBuilderSamples.Tests.Framework;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.BotBuilderSamples.Tests.Dialogs
{
    public class MainDialogTests : BotTestBase
    {
        private readonly BookingDialog _mockBookingDialog;
        private readonly Mock<ILogger<MainDialog>> _mockLogger;

        public MainDialogTests(ITestOutputHelper output)
            : base(output)
        {
            _mockLogger = new Mock<ILogger<MainDialog>>();

            var mockFlightBookingService = new Mock<IFlightBookingService>();
            mockFlightBookingService
                .Setup(x => x.BookFlight(It.IsAny<BookingDetails>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            _mockBookingDialog = SimpleMockFactory.CreateMockDialog<BookingDialog>(null, new Mock<GetBookingDetailsDialog>().Object, mockFlightBookingService.Object).Object;
        }

        [Fact]
        public void DialogConstructor()
        {
            var sut = new MainDialog(_mockLogger.Object, null, _mockBookingDialog);

            Assert.Equal("MainDialog", sut.Id);
            Assert.IsType<TextPrompt>(sut.FindDialog("TextPrompt"));
            Assert.NotNull(sut.FindDialog("BookingDialog"));
            Assert.IsType<WaterfallDialog>(sut.FindDialog("WaterfallDialog"));
        }

        [Fact]
        public async Task ShowsMessageIfLuisNotConfigured()
        {
            // Arrange
            var sut = new MainDialog(_mockLogger.Object, null, _mockBookingDialog);
            var testClient = new DialogTestClient(Channels.Test, sut, middlewares: new[] { new XUnitDialogTestLogger(Output) });

            // Act/Assert
            var reply = await testClient.SendActivityAsync<Activity>("hi");
            Assert.Equal("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", reply.Text);

            reply = testClient.GetNextReply<Activity>();
            Assert.Equal("What can I help you with today?", reply.Text);
        }

        [Fact]
        public async Task ShowsPromptIfLuisIsConfigured()
        {
            // Arrange
            var sut = new MainDialog(_mockLogger.Object, SimpleMockFactory.CreateMockLuisRecognizer<IRecognizer>(null).Object, _mockBookingDialog);
            var testClient = new DialogTestClient(Channels.Test, sut, middlewares: new[] { new XUnitDialogTestLogger(Output) });

            // Act/Assert
            var reply = await testClient.SendActivityAsync<Activity>("hi");
            Assert.Equal("What can I help you with today?", reply.Text);
        }
    }
}
