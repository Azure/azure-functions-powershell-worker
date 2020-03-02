//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;

    using Microsoft.Azure.Functions.PowerShellWorker.Durable;

    using Moq;
    using Xunit;

    public class OrchestrationInvokerTests
    {
        readonly OrchestrationInvoker _orchestrationInvoker = new OrchestrationInvoker();

        readonly OrchestrationBindingInfo _orchestrationBindingInfo = new OrchestrationBindingInfo(
                                                                        "ContextParameterName",
                                                                        new OrchestrationContext());

        private readonly Mock<IPowerShellServices> _mockPowerShellServices = new Mock<IPowerShellServices>();

        [Fact]
        public void InvocationRunsToCompletionIfNotStopped()
        {
            var invocationAsyncResult = CreateInvocationResult(completed: true);
            ExpectBeginInvoke(invocationAsyncResult);

            _orchestrationInvoker.Invoke(_orchestrationBindingInfo, _mockPowerShellServices.Object);

            _mockPowerShellServices.Verify(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()), Times.Once);
            _mockPowerShellServices.Verify(_ => _.EndInvoke(invocationAsyncResult), Times.Once);
            _mockPowerShellServices.Verify(_ => _.ClearStreamsAndCommands(), Times.Once);
            _mockPowerShellServices.VerifyNoOtherCalls();
        }

        [Fact]
        public void InvocationStopsOnStopEvent()
        {
            InvokeOrchestration(completed: false);

            _mockPowerShellServices.Verify(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()), Times.Once);
            _mockPowerShellServices.Verify(_ => _.StopInvoke(), Times.Once);
            _mockPowerShellServices.Verify(_ => _.ClearStreamsAndCommands(), Times.Once);
            _mockPowerShellServices.VerifyNoOtherCalls();
        }

        [Fact]
        public void ConsiderOrchestrationDoneWhenNotStopped()
        {
            var result = InvokeOrchestration(completed: true);

            Assert.Single(result);
            var returnOrchestrationMessage = (OrchestrationMessage)result["$return"];
            Assert.True(returnOrchestrationMessage.IsDone);
        }

        [Fact]
        public void ConsiderOrchestrationNotDoneWhenStopped()
        {
            var result = InvokeOrchestration(completed: false);

            Assert.Single(result);
            var returnOrchestrationMessage = (OrchestrationMessage)result["$return"];
            Assert.False(returnOrchestrationMessage.IsDone);
        }

        [Theory]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 5)]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(false, 5)]
        public void ReturnsOrchestrationActions(bool completed, int actionCount)
        {
            var actions = Enumerable.Range(0, actionCount)
                            .Select(i => new CallActivityAction($"activity{i}", $"input{i}"))
                            .ToArray();

            foreach (var action in actions)
            {
                _orchestrationBindingInfo.Context.OrchestrationActionCollector.Add(action);
            }

            var result = InvokeOrchestration(completed);

            Assert.Single(result);
            var returnOrchestrationMessage = (OrchestrationMessage)result["$return"];
            Assert.Equal(actions.Length, returnOrchestrationMessage.Actions.Count);
            Assert.All(returnOrchestrationMessage.Actions, actionEntry => Assert.Single(actionEntry));
            Assert.Equal(actions, returnOrchestrationMessage.Actions.SelectMany(x => x));
        }

        [Fact]
        public void ReturnsInvocationOutputWhenCompleted()
        {
            var output = new[] { "item1", "item2" };
            var result = InvokeOrchestration(completed: true, output);

            Assert.Single(result);
            var returnOrchestrationMessage = (OrchestrationMessage)result["$return"];
            Assert.Equal(output, returnOrchestrationMessage.Output);
        }

        [Fact]
        public void ReturnsNoInvocationOutputWhenStopped()
        {
            var output = new[] { "item1", "item2" };
            var result = InvokeOrchestration(completed: false, output);

            Assert.Single(result);
            var returnOrchestrationMessage = (OrchestrationMessage)result["$return"];
            Assert.Null(returnOrchestrationMessage.Output);
        }

        private Hashtable InvokeOrchestration(bool completed, PSDataCollection<object> output = null)
        {
            var invocationAsyncResult = CreateInvocationResult(completed);
            ExpectBeginInvoke(invocationAsyncResult, output);
            if (!completed)
            {
                SignalToStopInvocation();
            }

            var result = _orchestrationInvoker.Invoke(_orchestrationBindingInfo, _mockPowerShellServices.Object);
            return result;
        }

        private static IAsyncResult CreateInvocationResult(bool completed)
        {
            var completionEvent = new AutoResetEvent(initialState: completed);
            var result = new Mock<IAsyncResult>();
            result.Setup(_ => _.AsyncWaitHandle).Returns(completionEvent);
            return result.Object;
        }

        private void ExpectBeginInvoke(IAsyncResult invocationAsyncResult, PSDataCollection<object> output = null)
        {
            _mockPowerShellServices
                .Setup(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()))
                .Returns(
                    (PSDataCollection<object> outputBuffer) =>
                    {
                        if (output != null)
                        {
                            foreach (var item in output)
                            {
                                outputBuffer.Add(item);
                            }
                        }

                        return invocationAsyncResult;
                    });
        }

        private void SignalToStopInvocation()
        {
            _orchestrationBindingInfo.Context.OrchestrationActionCollector.Stop();
        }
    }
}
