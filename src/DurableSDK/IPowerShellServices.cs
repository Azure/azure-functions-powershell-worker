//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
    using System;
    using System.Management.Automation;

    internal interface IPowerShellServices
    {
        PowerShell GetPowerShell();

        bool UseExternalDurableSDK();

        void SetDurableClient(object durableClient);

        OrchestrationBindingInfo SetOrchestrationContext(ParameterBinding orchestrationContext, out Action<object> externalInvoker);

        void ClearOrchestrationContext();

        void TracePipelineObject();

        void AddParameter(string name, object value);

        IAsyncResult BeginInvoke(PSDataCollection<object> output);

        void EndInvoke(IAsyncResult asyncResult);

        void StopInvoke();

        void ClearStreamsAndCommands();
    }
}
