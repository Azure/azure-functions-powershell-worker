//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// OrchestrationFailureException should be propagated back to the Host when an orchestrator function
    /// throws and does not handle an exception. The Durable Functions extension implementation requires
    /// this exception message to contain the Json-serialized orchestration replay state after a special marker.
    /// </summary>
    internal class OrchestrationFailureException : Exception
    {
        public const string OutOfProcDataLabel = "\n\n$OutOfProcData$:";

        public OrchestrationFailureException()
        {
        }

        public OrchestrationFailureException(List<OrchestrationAction> actions, Exception innerException)
            : base(FormatOrchestrationFailureMessage(actions, innerException), innerException)
        {
        }

        private static string FormatOrchestrationFailureMessage(List<OrchestrationAction> actions, Exception exception)
        {
            // For more details on why this message looks like this, see:
            // - https://github.com/Azure/azure-functions-durable-js/pull/145
            // - https://github.com/Azure/azure-functions-durable-extension/pull/1171
            var orchestrationMessage = new OrchestrationMessage(isDone: false, new List<List<OrchestrationAction>> { actions }, output: null, exception.Message);
            var message = $"{exception.Message}{OutOfProcDataLabel}{JsonConvert.SerializeObject(orchestrationMessage)}";
            return message;
        }
    }
}
