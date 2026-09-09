//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a SignalR connection info input binding.
    /// </summary>
    public sealed class SignalRInputAttribute : InputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "signalRConnectionInfo";

        /// <summary>The HubName property.</summary>
        public string HubName { get; set; }
        /// <summary>The UserId property.</summary>
        public string UserId { get; set; }
        /// <summary>The IdToken property.</summary>
        public string IdToken { get; set; }
        /// <summary>The ClaimTypeList property.</summary>
        public string ClaimTypeList { get; set; }
    }
}
