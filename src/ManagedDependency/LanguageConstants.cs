// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.PowerShellWorker.ManagedDependency
{
    /// <summary>
    /// Constants used by powershell worker
    /// </summary>
    internal class LanguageConstants
    {
        /// <summary>
        /// Name of the function app host file
        /// </summary>
        public const string HostFileName = "host.json";
        /// <summary>
        /// Property name for for managed dependencies library name and version in host.json file
        /// </summary>
        public const string ManagedDepenciesPropertyName = "managedDependencies";

        /// <summary>
        /// The path where latest AzModules are located on VM
        /// </summary>
        public const string LatestAzModulePath = @"D:\Program Files (x86)\ManagedDependencies\PowerShell\AzPSModules\1.0.0";
    }
}
