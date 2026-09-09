//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a queue trigger binding.
    /// </summary>
    public sealed class QueueTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "queueTrigger";

        /// <inheritdoc />
        public override string RequiredProperties => "QueueName";

        /// <summary>
        /// The name of the queue to monitor.
        /// </summary>
        public string QueueName { get; set; }
    }
}
