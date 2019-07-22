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
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

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
        /// Gets the concurrency upper bound.
        /// </summary>
        internal int UpperBound => _upperBound;

        /// <summary>
        /// Constructor of the pool.
        /// </summary>
        internal PowerShellManagerPool(MessagingStream msgStream)
        {
            string upperBound = Environment.GetEnvironmentVariable("PSWorkerInProcConcurrencyUpperBound");
            if (string.IsNullOrEmpty(upperBound) || !int.TryParse(upperBound, out _upperBound))
            {
                _upperBound = 1;
            }

            _msgStream = msgStream;
            _pool = new BlockingCollection<PowerShellManager>(_upperBound);
            RpcLogger.WriteSystemLog(LogLevel.Information, string.Format(PowerShellWorkerStrings.LogConcurrencyUpperBound, _upperBound.ToString()));
        }

        /// <summary>
        /// Populate the pool with the very first PowerShellManager instance.
        /// We instantiate PowerShellManager instances in a lazy way, starting from size 1 and increase the number of workers as needed.
        /// </summary>
        internal void Initialize(PowerShell pwsh)
        {
            var logger = new RpcLogger(_msgStream);
            var psManager = new PowerShellManager(logger, pwsh);
            _pool.Add(psManager);
            _poolSize = 1;
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
                if (_poolSize < _upperBound)
                {
                    int id = Interlocked.Increment(ref _poolSize);
                    if (id <= _upperBound)
                    {
                        // If the pool hasn't reached its bounded capacity yet, then
                        // we create a new item and return it.
                        var logger = new RpcLogger(_msgStream);
                        logger.SetContext(requestId, invocationId);
                        psManager = new PowerShellManager(logger, id);

                        RpcLogger.WriteSystemLog(LogLevel.Trace, string.Format(PowerShellWorkerStrings.LogNewPowerShellManagerCreated, id.ToString()));
                    }
                }

                if (psManager == null)
                {
                    // If the pool has reached its bounded capacity, then the thread
                    // should be blocked until an idle one becomes available.
                    psManager = _pool.Take();
                }
            }

            // Finish the initialization if not yet.
            // This applies only to the very first PowerShellManager instance, whose initialization was deferred.
            psManager.Initialize();

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
