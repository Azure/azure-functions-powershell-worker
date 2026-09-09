//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a timer trigger binding.
    /// </summary>
    public sealed class TimerTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "timerTrigger";

        /// <inheritdoc />
        public override string RequiredProperties => "Schedule";

        /// <summary>
        /// A CRON expression or TimeSpan string representing the timer schedule.
        /// </summary>
        public string Schedule { get; set; }

        /// <summary>
        /// Whether the function should be invoked immediately on startup.
        /// </summary>
        public bool RunOnStartup { get; set; }

        /// <summary>
        /// Whether to use the schedule monitor to ensure schedules are maintained.
        /// </summary>
        public bool UseMonitor { get; set; } = true;
    }
}
