//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines an explicit HTTP response output binding.
    /// For most HTTP functions this is implicit and not needed.
    /// </summary>
    public sealed class HttpOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "http";
    }
}
