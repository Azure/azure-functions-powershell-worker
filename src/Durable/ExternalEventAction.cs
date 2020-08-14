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

        public ExternalEventAction(string eventName)
            : base(ActionType.CallActivity)
        {
            EventName = eventName; 
        }
    }
}
