//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.Action
{
    /// <summary>
    /// Action types
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// Call an activity function.
        /// </summary>
        CallActivity = 0,

        /// <summary>
        /// Call an activity function with retry.
        /// </summary>
        CallActivityWithRetry = 1,

        /// <summary>
        /// Call a sub-orchestration function.
        /// </summary>
        CallSubOrchestrator = 2,

        /// <summary>
        /// Call a sub-orchestration function with retry.
        /// </summary>
        CallSubOrchestratorWithRetry = 3,

        /// <summary>
        /// Run the orchestration function as a loop.
        /// </summary>
        ContinueAsNew = 4,

        /// <summary>
        /// Create a timer.
        /// </summary>
        CreateTimer = 5,

        /// <summary>
        /// Wait for an external event.
        /// </summary>
        WaitForExternalEvent = 6,
    }

    /// <summary>
    /// Base class that represents an orchestration action.
    /// </summary>
    public abstract class AzAction
    {
        /// <summary>
        /// Base constructor for creating an action.
        /// </summary>
        protected AzAction(ActionType actionType)
        {
            ActionType = actionType;
        }

        /// <summary>
        /// Action type.
        /// </summary>
        public readonly ActionType ActionType;
    }

    /// <summary>
    /// An orchestration action that represents calling an activity function.
    /// </summary>
    public class CallActivityAction : AzAction
    {
        /// <summary>
        /// The activity function name.
        /// </summary>
        public readonly string FunctionName;
        
        /// <summary>
        /// The input to the activity function.
        /// </summary>
        public readonly object Input;

        /// <summary>
        /// Constructor
        /// </summary>
        internal CallActivityAction(string functionName, object input) : base(ActionType.CallActivity)
        {
            FunctionName = functionName;
            Input = input;
        }
    }

    /// <summary>
    /// An orchestration action that represents creating a timer.
    /// </summary>
    public class CreateTimerAction : AzAction
    {
        /// <summary>
        /// Time to fire the timer.
        /// </summary>
        public readonly DateTime FireAt;

        /// <summary>
        /// Indicate if the timer is cancelled.
        /// </summary>
        public readonly bool IsCanceled;

        /// <summary>
        /// Constructor.
        /// </summary>
        internal CreateTimerAction(DateTime fireAt, bool isCanceled) : base(ActionType.CreateTimer)
        {
            FireAt = fireAt;
            IsCanceled = isCanceled;
        }
    }

    /// <summary>
    /// An orchestration action that represents waiting for an external event.
    /// </summary>
    public class WaitForExternalEventAction : AzAction
    {
        /// <summary>
        /// Name of the external event.
        /// </summary>
        public readonly string ExternalEventName;

        /// <summary>
        /// Constructor.
        /// </summary>
        internal WaitForExternalEventAction(string externalEventName) : base(ActionType.WaitForExternalEvent)
        {
            ExternalEventName = externalEventName;
        }
    }
}
