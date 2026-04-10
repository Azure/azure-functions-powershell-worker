//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a SignalR trigger binding (for serverless SignalR upstream).
    /// </summary>
    public sealed class SignalRTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "signalRTrigger";

        /// <summary>The HubName property.</summary>
        public string HubName { get; set; }
        /// <summary>The Category property.</summary>
        public string Category { get; set; }
        /// <summary>The Event property.</summary>
        public string Event { get; set; }
    }
}
