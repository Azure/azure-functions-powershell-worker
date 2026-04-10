//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Durable Functions activity trigger binding.
    /// </summary>
    public sealed class ActivityTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "activityTrigger";
    }
}
