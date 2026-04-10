//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a table output binding.
    /// </summary>
    public sealed class TableOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "table";

        /// <summary>
        /// The name of the storage table.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The row key for the entity to write.
        /// </summary>
        public string RowKey { get; set; }

        /// <summary>
        /// The partition key for the entity to write.
        /// </summary>
        public string PartitionKey { get; set; }
    }
}
