//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Attributes
{
    /// <summary>
    /// Defines a Cosmos DB trigger binding.
    /// </summary>
    public sealed class CosmosDBTriggerAttribute : TriggerBindingBaseAttribute
    {
        /// <inheritdoc />
        public override string BindingType => "cosmosDBTrigger";

        /// <inheritdoc />
        public override string RequiredProperties => "DatabaseName,ContainerName";

        /// <summary>The DatabaseName property.</summary>
        public string DatabaseName { get; set; }
        /// <summary>The ContainerName property.</summary>
        public string ContainerName { get; set; }
        /// <summary>The LeaseContainerName property.</summary>
        public string LeaseContainerName { get; set; }
        /// <summary>The CreateLeaseContainerIfNotExists property.</summary>
        public bool CreateLeaseContainerIfNotExists { get; set; }
        /// <summary>The LeaseContainerPrefix property.</summary>
        public string LeaseContainerPrefix { get; set; }
        /// <summary>The FeedPollDelay property.</summary>
        public int FeedPollDelay { get; set; } = -1;
        /// <summary>The StartFromBeginning property.</summary>
        public bool StartFromBeginning { get; set; }
        /// <summary>The MaxItemsPerInvocation property.</summary>
        public int MaxItemsPerInvocation { get; set; } = -1;
        /// <summary>The PreferredLocations property.</summary>
        public string PreferredLocations { get; set; }
    }
}
