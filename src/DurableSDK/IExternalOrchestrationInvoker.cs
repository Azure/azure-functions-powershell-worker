//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections;

    // Contract interface for the orchestration invoker in the external Durable Functions SDK
    internal interface IExternalOrchestrationInvoker
    {
        // Invokes an orchestration using the external Durable SDK
        Hashtable Invoke(IPowerShellServices powerShellServices);
    }
}
