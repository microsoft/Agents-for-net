// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Builder.TestBot.Shared;
using Microsoft.Agents.Builder.TestBot.Shared.Dialogs;
using Microsoft.Agents.Builder.TestBot.Shared.Services;
using Microsoft.Agents.Core.Models;
using Microsoft.BuilderSamples.Tests.Framework;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.BuilderSamples.Tests.Dialogs
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
            var sut = new MainDialog(_mockLogger.Object,  _mockBookingDialog);

            Assert.Equal("MainDialog", sut.Id);
            Assert.IsType<TextPrompt>(sut.FindDialog("TextPrompt"));
            Assert.NotNull(sut.FindDialog("BookingDialog"));
            Assert.IsType<WaterfallDialog>(sut.FindDialog("WaterfallDialog"));
        }
    }
}
