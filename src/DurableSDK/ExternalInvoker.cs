//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections;
    using System.Management.Automation;

    internal class ExternalInvoker : IExternalInvoker
    {
        private readonly Func<PowerShell, object> _externalSDKInvokerFunction;

        public ExternalInvoker(Func<PowerShell, object> invokerFunction)
        {
            _externalSDKInvokerFunction = invokerFunction;
        }

        public Hashtable Invoke(IPowerShellServices powerShellServices)
        {
            return (Hashtable)_externalSDKInvokerFunction.Invoke(powerShellServices.GetPowerShell());
        }
    }
}
