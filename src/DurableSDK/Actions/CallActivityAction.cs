//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions
{
    /// <summary>
    /// An orchestration action that represents calling an activity function.
    /// </summary>
    internal class CallActivityAction : OrchestrationAction
    {
        /// <summary>
        /// The activity function name.
        /// </summary>
        public readonly string FunctionName;
        
        /// <summary>
        /// The input to the activity function.
        /// </summary>
        public readonly object Input;

        public CallActivityAction(string functionName, object input)
            : base(ActionType.CallActivity)
        {
            FunctionName = functionName;
            Input = input;
        }
    }
}
