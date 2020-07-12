//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Mixing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    public class DurableTimerTask
    {
        public bool IsCompleted { get; }
        public bool IsCanceled { get; set; }

        private readonly CreateDurableTimerAction _createDurableTimerAction;

        public DurableTimerTask(
            bool isCompleted,
            bool isCanceled,
            DateTime fireAt)
            : this(
                isCompleted,
                isCanceled,
                new CreateDurableTimerAction(fireAt, isCanceled))
        {
        }

        internal DurableTimerTask(
            bool isCompleted,
            bool isCanceled,
            CreateDurableTimerAction createDurableTimerAction)
            {
                IsCompleted = isCompleted;
                IsCanceled = isCanceled;
                _createDurableTimerAction = createDurableTimerAction;
            }

        public void Stop()
        {  
            if (!IsCompleted)
            {
                IsCanceled = true;
            }
            else
            {
                throw new InvalidOperationException("Cannot cancel a completed task.");
            }
        }
    }
}
