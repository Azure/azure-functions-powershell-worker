//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections;
    using System.Management.Automation;

    internal interface IOrchestrationInvoker
    {
        Hashtable Invoke(OrchestrationBindingInfo orchestrationBindingInfo, IPowerShellServices pwsh);
        void SetExternalInvoker(Action<PowerShell> externalInvoker);
    }
}
