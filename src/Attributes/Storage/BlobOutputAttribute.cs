//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a blob output binding.
    /// </summary>
    public sealed class BlobOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "blob";

        /// <summary>
        /// The blob path to write to (e.g., "container/{name}").
        /// </summary>
        public string Path { get; set; }
    }
}
