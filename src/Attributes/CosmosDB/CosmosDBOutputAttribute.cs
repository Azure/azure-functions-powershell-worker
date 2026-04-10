//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Cosmos DB output binding.
    /// </summary>
    public sealed class CosmosDBOutputAttribute : OutputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "cosmosDB";

        /// <summary>The DatabaseName property.</summary>
        public string DatabaseName { get; set; }
        /// <summary>The ContainerName property.</summary>
        public string ContainerName { get; set; }
        /// <summary>The CreateIfNotExists property.</summary>
        public bool CreateIfNotExists { get; set; }
        /// <summary>The PartitionKey property.</summary>
        public string PartitionKey { get; set; }
        /// <summary>The PreferredLocations property.</summary>
        public string PreferredLocations { get; set; }
    }
}
