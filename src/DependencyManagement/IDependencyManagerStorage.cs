//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Collections.Generic;

    internal interface IDependencyManagerStorage
    {
        IEnumerable<DependencyManifestEntry> GetDependencies();

        bool SnapshotExists(string name);

        IEnumerable<string> GetInstalledSnapshots();

        IEnumerable<string> GetInstalledAndInstallingSnapshots();

        IEnumerable<string> GetInstalledModuleVersions(string snapshotPath, string moduleName, string majorVersion);

        string CreateNewSnapshotPath();

        string CreateInstallingSnapshot(string path);

        void PromoteInstallingSnapshotToInstalledAtomically(string path);

        void RemoveSnapshot(string path);

        DateTime GetSnapshotCreationTimeUtc(string path);
    }
}
