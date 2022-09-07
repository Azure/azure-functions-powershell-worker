//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DurableWorker
{
    using System;

    internal static class DurableBindings
    {
        private const string DurableClient = "durableClient";
        private const string OrchestrationTrigger = "orchestrationTrigger";
        private const string ActivityTrigger = "activityTrigger";

        // For Durable v1 only
        private const string OrchestrationClient = "orchestrationClient";

        public static bool IsDurableClient(string bindingType)
        {
            return string.Compare(bindingType, DurableClient, StringComparison.OrdinalIgnoreCase) == 0
                   || string.Compare(bindingType, OrchestrationClient, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static bool IsOrchestrationTrigger(string bindingType)
        {
            return string.Compare(bindingType, OrchestrationTrigger, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static bool IsActivityTrigger(string bindingType)
        {
            return string.Compare(bindingType, ActivityTrigger, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static bool CanParameterDeclarationBeOmitted(string bindingType)
        {
            // Declaring a function parameter for the orchestration client binding is allowed but not mandatory
            return IsDurableClient(bindingType);
        }
    }
}
