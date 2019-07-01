//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System.Management.Automation;

    internal interface IModuleProvider
    {
        /// <summary>
        /// Returns the latest published module version for the given module name and major version.
        /// </summary>
        string GetLatestPublishedModuleVersion(string moduleName, string majorVersion);

        /// <summary>
        /// Save the specified module locally.
        /// </summary>
        void SaveModule(PowerShell pwsh, string moduleName, string version, string path);

        /// <summary>
        /// Clean up after installing modules.
        /// </summary>
        void Cleanup(PowerShell pwsh);
    }
}
