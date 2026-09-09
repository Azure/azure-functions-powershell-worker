//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a blob trigger binding.
    /// </summary>
    public sealed class BlobTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "blobTrigger";

        /// <inheritdoc />
        public override string RequiredProperties => "Path";

        /// <summary>
        /// The blob path to monitor (e.g., "container/{name}").
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The source of the blob trigger event ("EventGrid" or "LogsAndContainerScan").
        /// </summary>
        public string Source { get; set; }
    }
}
