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

    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;

    using Moq;
    using Xunit;

    public class OrchestrationInvokerTests
    {
        readonly OrchestrationInvoker _orchestrationInvoker = new OrchestrationInvoker();

        private static readonly HistoryEvent[] _history = { new HistoryEvent { 
                                                                            EventType = HistoryEventType.OrchestratorStarted,
                                                                            Timestamp = new DateTime() } };
        readonly OrchestrationBindingInfo _orchestrationBindingInfo = new OrchestrationBindingInfo(
                                                                        "ContextParameterName",
                                                                        new OrchestrationContext { History = _history });

        private readonly Mock<IPowerShellServices> _mockPowerShellServices = new Mock<IPowerShellServices>();

        [Fact]
        public void InvocationRunsToCompletionIfNotStopped()
        {
            var invocationAsyncResult = DurableTestUtilities.CreateInvocationResult(completed: true);
            DurableTestUtilities.ExpectBeginInvoke(_mockPowerShellServices, invocationAsyncResult);
            _mockPowerShellServices.Setup(_ => _.UseExternalDurableSDK()).Returns(false);

            _orchestrationInvoker.Invoke(_orchestrationBindingInfo, _mockPowerShellServices.Object);

            _mockPowerShellServices.Verify(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()), Times.Once);
            _mockPowerShellServices.Verify(_ => _.EndInvoke(invocationAsyncResult), Times.Once);
            _mockPowerShellServices.Verify(_ => _.ClearStreamsAndCommands(), Times.Once);
            _mockPowerShellServices.Verify(_ => _.TracePipelineObject(), Times.Once);
            _mockPowerShellServices.Verify(_ => _.AddParameter(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
            _mockPowerShellServices.Verify(_ => _.UseExternalDurableSDK(), Times.Once);
            _mockPowerShellServices.VerifyNoOtherCalls();
        }

        [Fact]
        public void InvocationStopsOnStopEvent()
        {
            InvokeOrchestration(completed: false);
            _mockPowerShellServices.Setup(_ => _.UseExternalDurableSDK()).Returns(false);

            _mockPowerShellServices.Verify(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()), Times.Once);
            _mockPowerShellServices.Verify(_ => _.StopInvoke(), Times.Once);
            _mockPowerShellServices.Verify(_ => _.ClearStreamsAndCommands(), Times.Once);
            _mockPowerShellServices.Verify(_ => _.TracePipelineObject(), Times.Once);
            _mockPowerShellServices.Verify(_ => _.AddParameter(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
            _mockPowerShellServices.Verify(_ => _.UseExternalDurableSDK(), Times.Once);
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
            if (actionCount == 0)
            {
                Assert.Empty(returnOrchestrationMessage.Actions);
            }
            else
            {
                Assert.Single(returnOrchestrationMessage.Actions);
                Assert.Equal(actions.Length, returnOrchestrationMessage.Actions.Single().Count);
                Assert.Equal(actions, returnOrchestrationMessage.Actions.Single());
            }
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

        [Fact]
        public void WrapsExceptionIntoOrchestrationFailureException()
        {
            Exception originalException = new Exception("Original exception");
            _mockPowerShellServices.Setup(_ => _.EndInvoke(It.IsAny<IAsyncResult>())).Throws(originalException);

            var output = new[] { "item1", "item2" };

            var thrownException = Assert.Throws<OrchestrationFailureException>(() => InvokeOrchestration(completed: true, output));
            Assert.Same(originalException, thrownException.InnerException);
        }

        private Hashtable InvokeOrchestration(bool completed, PSDataCollection<object> output = null)
        {
            return DurableTestUtilities.InvokeOrchestration(_orchestrationInvoker, _orchestrationBindingInfo, _mockPowerShellServices, completed, output);
        }
    }
}
