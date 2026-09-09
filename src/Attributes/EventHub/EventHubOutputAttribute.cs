//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines an Event Hub output binding.
    /// </summary>
    public sealed class EventHubOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "eventHub";

        /// <summary>The EventHubName property.</summary>
        public string EventHubName { get; set; }
    }
}
