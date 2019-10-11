//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    /// <summary>
    /// An orchestration action that represents waiting for an external event.
    /// </summary>
    internal class WaitForExternalEventAction : OrchestrationAction
    {
        /// <summary>
        /// Name of the external event.
        /// </summary>
        public readonly string ExternalEventName;

        public WaitForExternalEventAction(string externalEventName)
            : base(ActionType.WaitForExternalEvent)
        {
            ExternalEventName = externalEventName;
        }
    }
}
