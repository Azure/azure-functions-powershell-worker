//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    using System.Collections.Generic;
    using System.Linq;

    internal static class FunctionReturnValueBuilder
    {
        public static object CreateReturnValueFromFunctionOutput(IList<object> pipelineItems)
        {
            if (pipelineItems == null || pipelineItems.Count <= 0)
            {
                return null;
            }
            return pipelineItems.Count == 1 ? pipelineItems[0] : pipelineItems.ToArray();
        }
    }
}
