//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class FunctionInfoUtilities
    {
        private const string assistantSkillTriggerName = "assistantSkillTrigger";

        public static bool hasAssistantSkillTrigger(AzFunctionInfo info)
        {
            return info.InputBindings.Where(x => x.Value.Type == assistantSkillTriggerName).Any();
        }
    }
}
