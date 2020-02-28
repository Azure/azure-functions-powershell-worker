//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    internal class OrchestrationActionCollector
    {
        private readonly List<List<OrchestrationAction>> _actions = new List<List<OrchestrationAction>>();

        public AutoResetEvent StopEvent { get; } = new AutoResetEvent(initialState: false);

        public void Add(OrchestrationAction action)
        {
            _actions.Add(new List<OrchestrationAction> { action });
        }

        public Tuple<bool, List<List<OrchestrationAction>>> WaitForActions(WaitHandle completionWaitHandle)
        {
            var waitHandles = new[] { StopEvent, completionWaitHandle };
            var signaledHandleIndex = WaitHandle.WaitAny(waitHandles);
            var signaledHandle = waitHandles[signaledHandleIndex];
            var shouldStop = ReferenceEquals(signaledHandle, StopEvent);
            return Tuple.Create(shouldStop, _actions);
        }
    }
}
