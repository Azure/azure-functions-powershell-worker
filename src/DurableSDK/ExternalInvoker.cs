//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Management.Automation;

    internal class ExternalInvoker : IExternalInvoker
    {
        private readonly Func<PowerShell, object> _externalSDKInvokerFunction;
        private readonly IPowerShellServices _powerShellServices;

        public ExternalInvoker(Func<PowerShell, object> invokerFunction, IPowerShellServices powerShellServices)
        {
            _externalSDKInvokerFunction = invokerFunction;
            _powerShellServices = powerShellServices;
        }

        public void Invoke()
        {
            _externalSDKInvokerFunction.Invoke(_powerShellServices.GetPowerShell());
        }
    }
}
