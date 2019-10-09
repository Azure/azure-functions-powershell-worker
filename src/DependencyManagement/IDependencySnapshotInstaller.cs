//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System.Collections.Generic;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal interface IDependencySnapshotInstaller
    {
        void InstallSnapshot(
            IEnumerable<DependencyManifestEntry> dependencies,
            string targetPath,
            PowerShell pwsh,
            DependencySnapshotInstallationMode installationMode,
            ILogger logger);
    }
}
