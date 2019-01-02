//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    /// <summary>
    /// The PowerShellManager pool for the in-proc concurrency support.
    /// </summary>
    internal class PowerShellManagerPool
    {
        private const int UpperBound = 25;
        private readonly MessagingStream _msgStream;
        private readonly BlockingCollection<PowerShellManager> _pool;
        private int _poolSize;

        /// <summary>
        /// Constructor of the pool.
        /// </summary>
        internal PowerShellManagerPool(MessagingStream msgStream)
        {
            _msgStream = msgStream;
            _pool = new BlockingCollection<PowerShellManager>(UpperBound);
        }

        /// <summary>
        /// Initialize the pool and populate it with PowerShellManager instances.
        /// When it's time to really implement this pool, we probably should instantiate PowerShellManager instances in a lazy way.
        /// Maybe start from size 1 and increase the number of workers as needed.
        /// </summary>
        internal void Initialize(string requestId)
        {
            var logger = new RpcLogger(_msgStream);

            try
            {
                logger.SetContext(requestId, invocationId: null);
                _pool.Add(new PowerShellManager(logger));
                _poolSize = _pool.Count;
            }
            finally
            {
                logger.ResetContext();
            }
        }

        /// <summary>
        /// Checkout an idle PowerShellManager instance.
        /// When it's time to really implement this pool, this method is supposed to block when there is no idle instance available.
        /// </summary>
        internal PowerShellManager CheckoutIdleWorker(StreamingMessage request, AzFunctionInfo functionInfo)
        {
            PowerShellManager psManager = null;
            string requestId = request.RequestId;
            string invocationId = request.InvocationRequest?.InvocationId;

            // If the pool has an idle one, just use it.
            if (!_pool.TryTake(out psManager))
            {
                // The pool doesn't have an idle one.
                if (_poolSize < UpperBound &&
                    Interlocked.Increment(ref _poolSize) <= UpperBound)
                {
                    // If the pool hasn't reached its bounded capacity yet, then
                    // we create a new item and return it.
                    var logger = new RpcLogger(_msgStream);
                    logger.SetContext(requestId, invocationId);
                    psManager = new PowerShellManager(logger);
                }
                else
                {
                    // If the pool has reached its bounded capacity, then the thread
                    // should be blocked until an idle one becomes available.
                    psManager = _pool.Take();
                }
            }

            // Register the function with the Runspace before returning the idle PowerShellManager.
            FunctionMetadata.RegisterFunctionMetadata(psManager.InstanceId, functionInfo);
            psManager.Logger.SetContext(requestId, invocationId);
            return psManager;
        }

        /// <summary>
        /// Return a used PowerShellManager instance to the pool.
        /// </summary>
        internal void ReclaimUsedWorker(PowerShellManager psManager)
        {
            if (psManager != null)
            {
                // Unregister the Runspace before reclaiming the used PowerShellManager.
                FunctionMetadata.UnregisterFunctionMetadata(psManager.InstanceId);
                psManager.Logger.ResetContext();
                _pool.Add(psManager);
            }
        }
    }
}
