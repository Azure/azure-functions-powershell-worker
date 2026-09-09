//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Service Bus output binding.
    /// </summary>
    public sealed class ServiceBusOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "serviceBus";

        /// <summary>The QueueName property.</summary>
        public string QueueName { get; set; }
        /// <summary>The TopicName property.</summary>
        public string TopicName { get; set; }
    }
}
