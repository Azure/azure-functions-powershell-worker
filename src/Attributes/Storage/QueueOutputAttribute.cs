//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a queue output binding.
    /// </summary>
    public sealed class QueueOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "queue";

        /// <summary>
        /// The name of the queue to write to.
        /// </summary>
        public string QueueName { get; set; }
    }
}
