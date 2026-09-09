//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a SignalR message output binding.
    /// </summary>
    public sealed class SignalROutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "signalR";

        /// <summary>The HubName property.</summary>
        public string HubName { get; set; }
    }
}
