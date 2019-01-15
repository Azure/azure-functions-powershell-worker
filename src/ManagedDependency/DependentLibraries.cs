// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    /// <summary>
    /// The dependent libraries declared in host.json file
    /// </summary>
    internal class DependentLibraries
    {
        /// <summary>
        /// Gets or sets the ManagedDependencies properties mentioned in the host.json file
        /// </summary>
        [JsonProperty("managedDependencies")]
        public IEnumerable<DependentLibrary> ManagedDependencies { get; set; }
    }
}
