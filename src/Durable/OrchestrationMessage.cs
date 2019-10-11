//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections.Generic;

    /// <summary>
    /// Represent an orchestration message to be sent to the host.
    /// </summary>
    internal class OrchestrationMessage
    {
        public OrchestrationMessage(bool isDone, List<List<OrchestrationAction>> actions, object output)
        {
            IsDone = isDone;
            Actions = actions;
            Output = output;
        }

        /// <summary>
        /// Indicate whether the orchestration is done.
        /// </summary>
        public readonly bool IsDone;

        /// <summary>
        /// Orchestration actions to be taken.
        /// </summary>
        public readonly List<List<OrchestrationAction>> Actions;

        /// <summary>
        /// The output result of the orchestration function run.
        /// </summary>
        public readonly object Output;
    }
}
