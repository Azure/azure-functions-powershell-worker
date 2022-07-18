//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections;

    internal interface IOrchestrationInvoker
    {
        Hashtable Invoke(OrchestrationBindingInfo orchestrationBindingInfo, IPowerShellServices pwsh);
        void SetExternalInvoker(IExternalOrchestrationInvoker externalInvoker);
    }
}
