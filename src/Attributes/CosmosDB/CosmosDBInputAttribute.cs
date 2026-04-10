//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Cosmos DB input binding.
    /// </summary>
    public sealed class CosmosDBInputAttribute : InputBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "cosmosDB";

        /// <summary>The DatabaseName property.</summary>
        public string DatabaseName { get; set; }
        /// <summary>The ContainerName property.</summary>
        public string ContainerName { get; set; }
        /// <summary>The Id property.</summary>
        public string Id { get; set; }
        /// <summary>The PartitionKey property.</summary>
        public string PartitionKey { get; set; }
        /// <summary>The SqlQuery property.</summary>
        public string SqlQuery { get; set; }
        /// <summary>The PreferredLocations property.</summary>
        public string PreferredLocations { get; set; }
    }
}
