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
        private readonly List<OrchestrationAction> _actions = new List<OrchestrationAction>();

        private readonly AutoResetEvent _stopEvent = new AutoResetEvent(initialState: false);

        public void Add(OrchestrationAction action)
        {
            _actions.Add(action);
        }

        public Tuple<bool, List<OrchestrationAction>> WaitForActions(WaitHandle completionWaitHandle)
        {
            var waitHandles = new[] { _stopEvent, completionWaitHandle };
            var signaledHandleIndex = WaitHandle.WaitAny(waitHandles);
            var signaledHandle = waitHandles[signaledHandleIndex];
            var shouldStop = ReferenceEquals(signaledHandle, _stopEvent);
            return Tuple.Create(shouldStop, _actions);
        }

        public void Stop()
        {
            _stopEvent.Set();
        }
    }
}
