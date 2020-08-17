//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    internal enum HistoryEventType
    {
        /// <summary>
        /// Orchestration execution has started event
        /// </summary>
        ExecutionStarted,

        /// <summary>
        /// Orchestration execution has completed event
        /// </summary>
        ExecutionCompleted,

        /// <summary>
        /// Orchestration execution has failed event
        /// </summary>
        ExecutionFailed,

        /// <summary>
        /// Orchestration was terminated event
        /// </summary>
        ExecutionTerminated,

        /// <summary>
        /// Task Activity scheduled event 
        /// </summary>
        TaskScheduled,

        /// <summary>
        /// Task Activity completed event
        /// </summary>
        TaskCompleted,

        /// <summary>
        /// Task Activity failed event
        /// </summary>
        TaskFailed,

        /// <summary>
        /// Sub Orchestration instance created event
        /// </summary>
        SubOrchestrationInstanceCreated,

        /// <summary>
        /// Sub Orchestration instance completed event
        /// </summary>
        SubOrchestrationInstanceCompleted,

        /// <summary>
        /// Sub Orchestration instance failed event
        /// </summary>
        SubOrchestrationInstanceFailed,

        /// <summary>
        /// Timer created event
        /// </summary>
        TimerCreated,

        /// <summary>
        /// Timer fired event
        /// </summary>
        TimerFired,

        /// <summary>
        /// Orchestration has started event
        /// </summary>
        OrchestratorStarted,

        /// <summary>
        /// Orchestration has completed event
        /// </summary>
        OrchestratorCompleted,

        /// <summary>
        /// External Event sent by orchestration event
        /// </summary>
        EventSent,

        /// <summary>
        /// External Event raised to orchestration event
        /// </summary>
        EventRaised,

        /// <summary>
        /// Orchestration Continued as new event
        /// </summary>
        ContinueAsNew,

        /// <summary>
        /// Generic event for tracking event existence
        /// </summary>
        GenericEvent,

        /// <summary>
        /// Orchestration state history event
        /// </summary>
        HistoryState,
    }
}
