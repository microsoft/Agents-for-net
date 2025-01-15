// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.BotBuilder.TestBot.Shared;
using Microsoft.Agents.BotBuilder.TestBot.Shared.Dialogs;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Testing.XUnit;
using Microsoft.Agents.Core.Models;
using Microsoft.BotBuilderSamples.Tests.Dialogs.TestData;
using Microsoft.BotBuilderSamples.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.BotBuilderSamples.Tests.Dialogs
{
    public class GetBookingDetailsDialogTests : BotTestBase
    {
        public GetBookingDetailsDialogTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetBookingDetailsDialogTestsDataGenerator.BookingFlows), MemberType = typeof(GetBookingDetailsDialogTestsDataGenerator))]
        [MemberData(nameof(GetBookingDetailsDialogTestsDataGenerator.CancelFlows), MemberType = typeof(GetBookingDetailsDialogTestsDataGenerator))]
        public async Task DialogFlowUseCases(TestDataObject testData)
        {
            // Arrange
            var bookingTestData = testData.GetObject<GetBookingDetailsDialogTestCase>();
            var sut = new GetBookingDetailsDialog();
            var testClient = new DialogTestClient(Channels.Test, sut, bookingTestData.InitialBookingDetails, new[] { new XUnitDialogTestLogger(Output) });

            // Act/Assert
            Output.WriteLine($"Test Case: {bookingTestData.Name}");
            for (var i = 0; i < bookingTestData.UtterancesAndReplies.GetLength(0); i++)
            {
                var reply = await testClient.SendActivityAsync<Activity>(bookingTestData.UtterancesAndReplies[i, 0]);
                Assert.Equal(bookingTestData.UtterancesAndReplies[i, 1], reply?.Text);
            }

            if (testClient.DialogTurnResult.Result != null)
            {
                var bookingResults = (BookingDetails)testClient.DialogTurnResult.Result;
                Assert.Equal(bookingTestData.ExpectedBookingDetails.Origin, bookingResults.Origin);
                Assert.Equal(bookingTestData.ExpectedBookingDetails.Destination, bookingResults.Destination);
                Assert.Equal(bookingTestData.ExpectedBookingDetails.TravelDate, bookingResults.TravelDate);
            }
        }
    }
}
