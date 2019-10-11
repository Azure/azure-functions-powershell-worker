//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Threading;

    /// <summary>
    /// Represent the context of an execution of an orchestration function.
    /// </summary>
    [DataContract]
    public class OrchestrationContext
    {
        [DataMember]
        public object Input { get; internal set; }

        [DataMember]
        internal string InstanceId { get; set; }

        [DataMember]
        internal string ParentInstanceId { get; set; }

        [DataMember]
        internal bool IsReplaying { get; set; }

        [DataMember]
        internal HistoryEvent[] History { get; set; }

        internal AutoResetEvent ActionEvent { get; set; }

        internal List<List<OrchestrationAction>> Actions { get; } = new List<List<OrchestrationAction>>();

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
}
