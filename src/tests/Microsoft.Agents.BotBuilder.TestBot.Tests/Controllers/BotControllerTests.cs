﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Agents.BotBuilder.TestBot.Shared.Controllers;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;
using Microsoft.Agents.BotBuilder;

namespace Microsoft.BotBuilderSamples.Tests.Controllers
{
    public class BotControllerTests
    {
        [Fact]
        public async Task PostAsyncCallsProcessAsyncOnAdapter()
        {
            // Create MVC infrastructure mocks and objects
            var request = new Mock<HttpRequest>();
            var response = new Mock<HttpResponse>();
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(request.Object);
            mockHttpContext.Setup(x => x.Response).Returns(response.Object);
            var actionContext = new ActionContext(mockHttpContext.Object, new RouteData(), new ControllerActionDescriptor());

            // Create BF mocks
            var mockAdapter = new Mock<IBotHttpAdapter>();
            mockAdapter
                .Setup(x => x.ProcessAsync(It.IsAny<HttpRequest>(), It.IsAny<HttpResponse>(), It.IsAny<IBot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var mockBot = new Mock<IBot>();

            // Create and initialize controller
            var sut = new BotController(mockAdapter.Object, (s) => mockBot.Object)
            {
                ControllerContext = new ControllerContext(actionContext),
            };

            // Invoke the controller
            await sut.PostAsync("doesn't matter");

            // Assert
            mockAdapter.Verify(
                x => x.ProcessAsync(                    
                    It.Is<HttpRequest>(o => o == request.Object),
                    It.Is<HttpResponse>(o => o == response.Object),
                    It.Is<IBot>(o => o == mockBot.Object),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
