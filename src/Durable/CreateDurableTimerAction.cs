//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;

    /// <summary>
    /// An orchestration action that represents creating a Durable timer.
    /// </summary>
    internal class CreateDurableTimerAction : OrchestrationAction
    {
        /// <summary>
        /// The DateTime at which the timer will fire.
        /// </summary>
        public readonly DateTime FireAt;
        
        public CreateDurableTimerAction (DateTime fireAt)
            : base(ActionType.CreateTimer)
        {
            FireAt = fireAt;
        }
    }
}
