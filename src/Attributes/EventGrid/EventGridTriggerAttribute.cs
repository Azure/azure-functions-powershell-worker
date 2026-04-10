//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines an Event Grid trigger binding.
    /// </summary>
    public sealed class EventGridTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "eventGridTrigger";
    }
}
