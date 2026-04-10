//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Service Bus trigger binding.
    /// </summary>
    public sealed class ServiceBusTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "serviceBusTrigger";

        /// <summary>The QueueName property.</summary>
        public string QueueName { get; set; }
        /// <summary>The TopicName property.</summary>
        public string TopicName { get; set; }
        /// <summary>The SubscriptionName property.</summary>
        public string SubscriptionName { get; set; }
        /// <summary>The IsSessionsEnabled property.</summary>
        public bool IsSessionsEnabled { get; set; }
        /// <summary>
        /// "one" or "many". Defaults to "one".
        /// </summary>
        public string Cardinality { get; set; }
    }
}
