//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    /// <summary>
    /// The PowerShellManager pool for the in-proc concurrency support.
    /// </summary>
    internal class PowerShellManagerPool
    {
        private readonly ILogger _logger;
        // Today we don't really support the in-proc concurrency. We just hold an instance of PowerShellManager in this field.
        private PowerShellManager _psManager;

        /// <summary>
        /// Constructor of the pool.
        /// </summary>
        internal PowerShellManagerPool(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize the pool and populate it with PowerShellManager instances.
        /// When it's time to really implement this pool, we probably should instantiate PowerShellManager instances in a lazy way.
        /// Maybe start from size 1 and increase the number of workers as needed.
        /// </summary>
        internal void Initialize()
        {
            _psManager = new PowerShellManager(_logger);
        }

        /// <summary>
        /// Checkout an idle PowerShellManager instance.
        /// When it's time to really implement this pool, this method is supposed to block when there is no idle instance available.
        /// </summary>
        internal PowerShellManager CheckoutIdleWorker(AzFunctionInfo functionInfo)
        {
            // Register the function with the Runspace before returning the idle PowerShellManager.
            FunctionMetadata.RegisterFunctionMetadata(_psManager.InstanceId, functionInfo);
            return _psManager;
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
            }
        }
    }
}
