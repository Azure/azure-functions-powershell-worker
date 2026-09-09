//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines an Event Hub trigger binding.
    /// </summary>
    public sealed class EventHubTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "eventHubTrigger";

        /// <inheritdoc />
        public override string RequiredProperties => "EventHubName";

        /// <summary>The EventHubName property.</summary>
        public string EventHubName { get; set; }
        /// <summary>The ConsumerGroup property.</summary>
        public string ConsumerGroup { get; set; }
        /// <summary>
        /// "one" or "many". Defaults to "many".
        /// </summary>
        public string Cardinality { get; set; }
    }
}
