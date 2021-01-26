//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;
    using Xunit;

    public class OrchestrationActionCollectorTests
    {
        private readonly OrchestrationAction[] _expectedActions =
            Enumerable.Range(0, 14).Select(i => new CallActivityAction($"Name{i}", $"Input{i}")).ToArray();

        [Fact]
        public void IndicatesShouldNotStopOnSignalledCompletionWaitHandle()
        {
            var collector = new OrchestrationActionCollector();
            var (shouldStop, _) = collector.WaitForActions(new AutoResetEvent(initialState: true));
            Assert.False(shouldStop);
        }

        [Fact]
        public void IndicatesShouldStopOnStopEvent()
        {
            var collector = new OrchestrationActionCollector();
            collector.Stop();
            var (shouldStop, _) = collector.WaitForActions(new AutoResetEvent(initialState: false));
            Assert.True(shouldStop);
        }

        [Fact]
        public void ReturnsNoActionsWhenNoneAdded()
        {
            var collector = new OrchestrationActionCollector();
            var (_, actions) = collector.WaitForActions(new AutoResetEvent(initialState: true));
            Assert.Empty(actions);
        }

        [Fact]
        public void ReturnsSingleAction()
        {
            var collector = new OrchestrationActionCollector();
            collector.Add(_expectedActions[0]);
            var (_, actions) = collector.WaitForActions(new AutoResetEvent(initialState: true));

            Assert.Single(actions);
            Assert.Single(actions.Single());
            Assert.Same(_expectedActions[0], actions.Single().Single());
        }

        [Fact]
        public void ReturnsSequentialActions()
        {
            var collector = new OrchestrationActionCollector();

            collector.Add(_expectedActions[0]);
            collector.NextBatch();
            collector.Add(_expectedActions[1]);

            var (_, actions) = collector.WaitForActions(new AutoResetEvent(initialState: true));

            var expected = new[] {
                new[] { _expectedActions[0] },
                new[] { _expectedActions[1] }
            };

            AssertExpectedActions(expected, actions);
        }

        [Fact]
        public void ReturnsParallelActions()
        {
            var collector = new OrchestrationActionCollector();

            collector.Add(_expectedActions[0]);
            collector.Add(_expectedActions[1]);

            var (_, actions) = collector.WaitForActions(new AutoResetEvent(initialState: true));

            var expected = new[] {
                new[] { _expectedActions[0], _expectedActions[1] }
            };

            AssertExpectedActions(expected, actions);
        }

        [Fact]
        public void ReturnsMixOfSequentialAndParallelActions()
        {
            var collector = new OrchestrationActionCollector();

            collector.Add(_expectedActions[0]);
            collector.NextBatch();
            collector.Add(_expectedActions[1]);
            collector.Add(_expectedActions[2]);
            collector.NextBatch();
            collector.Add(_expectedActions[3]);
            collector.NextBatch();
            collector.Add(_expectedActions[4]);
            collector.Add(_expectedActions[5]);
            collector.Add(_expectedActions[6]);
            collector.NextBatch();
            collector.Add(_expectedActions[7]);
            collector.NextBatch();
            collector.Add(_expectedActions[8]);
            collector.NextBatch();
            collector.Add(_expectedActions[9]);
            collector.NextBatch();
            collector.Add(_expectedActions[10]);
            collector.Add(_expectedActions[11]);
            collector.Add(_expectedActions[12]);
            collector.Add(_expectedActions[13]);

            var (_, actions) = collector.WaitForActions(new AutoResetEvent(initialState: true));

            var expected = new[] {
                new[] { _expectedActions[0] },
                new[] { _expectedActions[1], _expectedActions[2] },
                new[] { _expectedActions[3] },
                new[] { _expectedActions[4], _expectedActions[5], _expectedActions[6] },
                new[] { _expectedActions[7] },
                new[] { _expectedActions[8] },
                new[] { _expectedActions[9] },
                new[] { _expectedActions[10], _expectedActions[11], _expectedActions[12], _expectedActions[13] }
            };

            AssertExpectedActions(expected, actions);
        }

        private void AssertExpectedActions(OrchestrationAction[][] expected, List<List<OrchestrationAction>> actual)
        {
            Assert.Equal(expected.Count(), actual.Count());
            for (var batchIndex = 0; batchIndex < expected.Count(); ++batchIndex)
            {
                for (var actionIndex = 0; actionIndex < expected[batchIndex].Count(); ++actionIndex)
                {
                    Assert.Same(expected[batchIndex][actionIndex], actual[batchIndex][actionIndex]);
                }
            }
        }
    }
}
