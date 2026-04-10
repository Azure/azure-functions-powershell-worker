//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines an Event Grid output binding.
    /// </summary>
    public sealed class EventGridOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "eventGrid";

        /// <summary>The TopicEndpointUri property.</summary>
        public string TopicEndpointUri { get; set; }
        /// <summary>The TopicKeySetting property.</summary>
        public string TopicKeySetting { get; set; }
    }
}
