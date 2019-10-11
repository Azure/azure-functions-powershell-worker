//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;

    /// <summary>
    /// An orchestration action that represents creating a timer.
    /// </summary>
    internal class CreateTimerAction : OrchestrationAction
    {
        /// <summary>
        /// Time to fire the timer.
        /// </summary>
        public readonly DateTime FireAt;

        /// <summary>
        /// Indicate if the timer is cancelled.
        /// </summary>
        public readonly bool IsCanceled;

        public CreateTimerAction(DateTime fireAt, bool isCanceled)
            : base(ActionType.CreateTimer)
        {
            FireAt = fireAt;
            IsCanceled = isCanceled;
        }
    }
}
