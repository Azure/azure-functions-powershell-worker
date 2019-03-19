//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly int _upperBound;
        private readonly MessagingStream _msgStream;
        private readonly BlockingCollection<PowerShellManager> _pool;
        private int _poolSize;

        /// <summary>
        /// Constructor of the pool.
        /// </summary>
        internal PowerShellManagerPool(MessagingStream msgStream)
        {
            string upperBound = Environment.GetEnvironmentVariable("PSWorkerInProcConcurrencyUpperBound");
            RpcLogger.WriteSystemLog(string.Format(PowerShellWorkerStrings.LogConcurrencyUpperBound, upperBound));

            if (string.IsNullOrEmpty(upperBound) || !int.TryParse(upperBound, out _upperBound))
            {
                _upperBound = 1;
            }

            _msgStream = msgStream;
            _pool = new BlockingCollection<PowerShellManager>(_upperBound);
        }

        /// <summary>
        /// Initialize the pool and populate it with PowerShellManager instances.
        /// We instantiate PowerShellManager instances in a lazy way, starting from size 1 and increase the number of workers as needed.
        /// </summary>
        internal void Initialize(string requestId, Action<PowerShell, ILogger> initAction = null)
        {
            var logger = new RpcLogger(_msgStream);

            try
            {
                logger.SetContext(requestId, invocationId: null);
                _pool.Add(new PowerShellManager(logger, initAction));
                _poolSize = 1;
            }
            finally
            {
                logger.ResetContext();
            }
        }

        /// <summary>
        /// Checkout an idle PowerShellManager instance in a non-blocking asynchronous way.
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
                if (_poolSize < _upperBound &&
                    Interlocked.Increment(ref _poolSize) <= _upperBound)
                {
                    // If the pool hasn't reached its bounded capacity yet, then
                    // we create a new item and return it.
                    var logger = new RpcLogger(_msgStream);
                    logger.SetContext(requestId, invocationId);
                    psManager = new PowerShellManager(logger);

                    RpcLogger.WriteSystemLog(string.Format(PowerShellWorkerStrings.LogNewPowerShellManagerCreated, _poolSize));
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
