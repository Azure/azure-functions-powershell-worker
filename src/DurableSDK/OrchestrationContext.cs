//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represent the context of an execution of an orchestration function.
    /// </summary>
    [DataContract]
    public class OrchestrationContext
    {
        [DataMember]
        public object Input { get; internal set; }

        [DataMember]
        public string InstanceId { get; set; }

        [DataMember]
        internal string ParentInstanceId { get; set; }

        [DataMember]
        public bool IsReplaying { get; set; }

        [DataMember]
        internal HistoryEvent[] History { get; set; }

        public DateTime CurrentUtcDateTime { get; internal set; }

        internal OrchestrationActionCollector OrchestrationActionCollector { get; } = new OrchestrationActionCollector();

        internal object ExternalResult;
        internal bool ExternalIsError;

        // Called by the External DF SDK to communicate its orchestration result
        // back to the worker.
        internal void SetExternalResult(object result, bool isError)
        {
            this.ExternalResult = result;
            this.ExternalIsError = isError;
        }

        internal object CustomStatus { get; set; }
    }
}
