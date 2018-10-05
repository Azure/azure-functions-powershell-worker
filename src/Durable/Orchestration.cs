//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

using Microsoft.Azure.Functions.PowerShellWorker.History;
using Microsoft.Azure.Functions.PowerShellWorker.Action;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Represent the context of an execution of an orchestration function.
    /// </summary>
    [DataContract]
    public class OrchestrationContext
    {
        [DataMember]
        internal object Input { get; set; }

        [DataMember]
        internal string InstanceId { get; set; }

        [DataMember]
        internal string ParentInstanceId { get; set; }

        [DataMember]
        internal bool IsReplaying { get; set; }

        [DataMember]
        internal HistoryEvent[] History { get; set; }

        internal AutoResetEvent ActionEvent { get; set; }

        internal List<List<AzAction>> Actions { get; } = new List<List<AzAction>>();

        /// <summary>
        /// Gets the input of the current orchestrator function as a deserialized value.
        /// </summary>
        /// <returns>The deserialized input value.</returns>
        public object GetInput()
        {
            return Input;
        }

        /// <summary>
        /// Gets the current date/time in a way that is safe for use by orchestrator functions.
        /// </summary>
        /// <remarks>
        /// This date/time value is derived from the orchestration history. It always returns the same value
        /// at specific points in the orchestrator function code, making it deterministic and safe for replay.
        /// </remarks>
        /// <value>The orchestration's current date/time in UTC.</value>
        public DateTime CurrentUtcDateTime { get; internal set; }
    }

    /// <summary>
    /// Represent an orchestration message to be sent to the host.
    /// </summary>
    public class OrchestrationMessage
    {
        internal OrchestrationMessage(bool isDone, List<List<AzAction>> actions, object output)
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
        public readonly List<List<AzAction>> Actions;

        /// <summary>
        /// The output result of the orchestration function run.
        /// </summary>
        public readonly object Output;
    }
}
