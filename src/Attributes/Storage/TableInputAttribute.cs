//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a table input binding.
    /// </summary>
    public sealed class TableInputAttribute : InputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "table";

        /// <summary>
        /// The name of the storage table.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The row key to read. If not specified, reads all rows matching the filter.
        /// </summary>
        public string RowKey { get; set; }

        /// <summary>
        /// The partition key to filter by.
        /// </summary>
        public string PartitionKey { get; set; }

        /// <summary>
        /// An OData filter expression for rows.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// The maximum number of rows to return.
        /// </summary>
        public int Take { get; set; } = -1;
    }
}
