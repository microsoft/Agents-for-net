// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Dialogs.Debugging;
using Microsoft.Agents.Core.Models;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests.Debugging
{
    [Collection("DebugSupport.SourceMap")]
    public class DebugSupportTests
    {
        [Fact]
        public void SourceMap_ShouldReturnDefaultValue()
        {
            Assert.IsType<NullSourceMap>(DebugSupport.SourceMap);
        }

        [Fact]
        public void SourceMap_ShouldReturnCustomValue()
        {
            var oldSourceMap = DebugSupport.SourceMap;
            var sourceMap = new SourceMap();
            
            DebugSupport.SourceMap = sourceMap;

            Assert.Equal(sourceMap, DebugSupport.SourceMap);

            DebugSupport.SourceMap = oldSourceMap;
        }

        [Fact]
        public async Task DebuggerStepAsync_ShouldCallStepAsyncPassingDialog()
        {
            var mockDialog = new Mock<Dialog>("testDialog");

            var debugger = new Mock<IDialogDebugger>();
            debugger.Setup(x => x.StepAsync(It.IsAny<DialogContext>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once);

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(tc => tc.Services).Returns([]);
            mockContext.Setup(tc => tc.StackState).Returns([]);
            mockContext.Object.Services.Set(debugger.Object);
            
            var dialogContext = new DialogContext(new DialogSet(), mockContext.Object, new DialogState());

            await dialogContext.DebuggerStepAsync(mockDialog.Object, "more", CancellationToken.None);

            Mock.Verify(debugger, mockContext);
        }

        [Fact]
        public async Task DebuggerStepAsync_ShouldCallStepAsyncPassingIRecognizer()
        {
            var mockRecognizer = new Mock<IRecognizer>();

            var debugger = new Mock<IDialogDebugger>();
            debugger.Setup(x => x.StepAsync(It.IsAny<DialogContext>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once);

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(tc => tc.Services).Returns([]).Verifiable(Times.Exactly(2));
            mockContext.Setup(tc => tc.StackState).Returns([]);
            mockContext.Object.Services.Set(debugger.Object);

            var dialogContext = new DialogContext(new DialogSet(), mockContext.Object, new DialogState());

            await dialogContext.DebuggerStepAsync(mockRecognizer.Object, "more", CancellationToken.None);

            Mock.Verify(debugger, mockContext);
        }

        [Fact]
        public async Task DebuggerStepAsync_ShouldCallStepAsyncPassingRecognizer()
        {
            var debugger = new Mock<IDialogDebugger>();
            debugger.Setup(x => x.StepAsync(It.IsAny<DialogContext>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once);

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(tc => tc.Services).Returns([]).Verifiable(Times.Exactly(2));
            mockContext.Setup(tc => tc.StackState).Returns([]);
            mockContext.Object.Services.Set(debugger.Object);

            var dialogContext = new DialogContext(new DialogSet(), mockContext.Object, new DialogState());

            await dialogContext.DebuggerStepAsync(new Recognizer(), "more", CancellationToken.None);

            Mock.Verify(debugger, mockContext);
        }
    }
}
