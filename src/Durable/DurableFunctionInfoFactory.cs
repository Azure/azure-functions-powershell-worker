//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Linq;

    using Google.Protobuf.Collections;
    using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

    internal static class DurableFunctionInfoFactory
    {
        public static DurableFunctionInfo Create(MapField<string, BindingInfo> bindings)
        {
            var clientBinding =
                bindings.FirstOrDefault(
                    binding => !string.IsNullOrEmpty(binding.Key)
                               && binding.Value.Direction == BindingInfo.Types.Direction.In
                               && DurableBindings.IsDurableClient(binding.Value.Type));

            var durableFunctionType = GetDurableFunctionType(bindings);

            return new DurableFunctionInfo(durableFunctionType, clientBinding.Key);
        }

        private static DurableFunctionType GetDurableFunctionType(MapField<string, BindingInfo> bindings)
        {
            var inputBindings = bindings.Where(binding => binding.Value.Direction == BindingInfo.Types.Direction.In);
            foreach (var (_, value) in inputBindings)
            {
                if (DurableBindings.IsOrchestrationTrigger(value.Type))
                {
                    return DurableFunctionType.OrchestrationFunction;
                }

                if (DurableBindings.IsActivityTrigger(value.Type))
                {
                    return DurableFunctionType.ActivityFunction;
                }
            }

            return DurableFunctionType.None;
        }
    }
}
