// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency
{
    /// <summary>
    /// Represents dependent library mentioned in the host.json file
    /// </summary>
    internal class DependentLibrary
    {
        /// <summary>
        /// Gets or sets the module name in host.json file
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the major version from host.json file 
        /// </summary>
        public string MajorVersion { get; set; }

        /// <summary>
        /// Gets or sets the flag indicating 
        /// if the managed library needs to be downloaded or not by the language worker
        /// </summary>
        public bool IsDownloadRequired { get; set; }

        /// <summary>
        /// Gets or sets the path where the managed library exist locally
        /// </summary>
        public string Path { get; set; }
    }
}
