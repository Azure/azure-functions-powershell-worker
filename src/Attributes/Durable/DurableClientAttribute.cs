//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Durable Functions client input binding (starter).
    /// </summary>
    public sealed class DurableClientAttribute : InputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "durableClient";

        /// <summary>
        /// The task hub name. Overrides the default task hub name from host.json.
        /// </summary>
        public string TaskHub { get; set; }

        /// <summary>
        /// The connection name for the durable storage backend.
        /// </summary>
        public string ConnectionName { get; set; }
    }
}
