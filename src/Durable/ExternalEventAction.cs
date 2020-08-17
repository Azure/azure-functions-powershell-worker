//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    /// <summary>
    /// An orchestration action that represents calling an activity function.
    /// </summary>
    internal class ExternalEventAction : OrchestrationAction
    {
        /// <summary>
        /// The external event name.
        /// </summary>
        public readonly string EventName;

        /// <summary>
        /// Reason for the action. This field is necessary for the Durable extension to recognize the ExternalEventAction.
        /// </summary>
        public readonly string Reason = "ExternalEvent";

        public ExternalEventAction(string eventName)
            : base(ActionType.WaitForExternalEvent)
        {
            EventName = eventName; 
        }
    }
}
