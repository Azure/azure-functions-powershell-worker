//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Newtonsoft.Json;
    using Xunit;

    public class OrchestrationFailureExceptionTests
    {
        private readonly Exception _innerException = new Exception("Inner exception message");

        [Fact]
        public void MessageContainsInnerExceptionMessage()
        {
            var e = new OrchestrationFailureException(new List<OrchestrationAction>(), _innerException);

            var labelPos = e.Message.IndexOf(OrchestrationFailureException.OutOfProcDataLabel);
            Assert.Equal(_innerException.Message, e.Message.Substring(0, labelPos));
        }

        [Fact]
        public void MessageContainsSerializedOrchestrationMessage()
        {
            var actions = new List<OrchestrationAction> {
                    new CallActivityAction("activity1", "input1"),
                    new CallActivityAction("activity2", "input2")
                };

            var e = new OrchestrationFailureException(actions, _innerException);

            var labelPos = e.Message.IndexOf(OrchestrationFailureException.OutOfProcDataLabel);
            var startPos = labelPos + OrchestrationFailureException.OutOfProcDataLabel.Length;
            var serialized = e.Message[startPos..];
            dynamic orchestrationMessage = JsonConvert.DeserializeObject(serialized);
            Assert.False((bool)orchestrationMessage.IsDone);
            Assert.Null(orchestrationMessage.Output.Value);
            Assert.Equal(_innerException.Message, (string)orchestrationMessage.Error);
            var deserializedActions = (IEnumerable<dynamic>)((IEnumerable<dynamic>)orchestrationMessage.Actions).Single();
            Assert.Equal(actions.Count(), deserializedActions.Count());
            for (var i = 0; i < actions.Count(); i++)
            {
                AssertEqualAction((OrchestrationAction)actions[i], deserializedActions.ElementAt(i));
            }
        }

        private static void AssertEqualAction(OrchestrationAction expected, dynamic actual)
        {
            Assert.Equal(((CallActivityAction)expected).FunctionName, (string)actual.FunctionName);
            Assert.Equal(((CallActivityAction)expected).Input, (string)actual.Input);
        }
    }
}
