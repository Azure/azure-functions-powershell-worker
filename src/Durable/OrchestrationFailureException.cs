//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

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
            var orchestrationMessage = new OrchestrationMessage(isDone: false, new List<List<OrchestrationAction>> { actions }, output: null, exception.Message);
            var message = $"{exception.Message}{OutOfProcDataLabel}{JsonConvert.SerializeObject(orchestrationMessage)}";
            return message;
        }
    }
}
