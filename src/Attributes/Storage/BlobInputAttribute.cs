//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a blob input binding.
    /// </summary>
    public sealed class BlobInputAttribute : InputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "blob";

        /// <summary>
        /// The blob path to read from (e.g., "container/{name}").
        /// </summary>
        public string Path { get; set; }
    }
}
