//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal interface IBackgroundDependencySnapshotMaintainer
    {
        void Start(string currentSnapshotPath, DependencyManifestEntry[] dependencyManifest, ILogger logger);

        /// <summary>
        /// Returns the path for the new dependencies snapshot.
        /// </summary>
        string InstallAndPurgeSnapshots(Func<PowerShell> pwshFactory, ILogger logger);
    }
}
