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

        string GetLatestInstalledSnapshot();

        IEnumerable<string> GetInstalledAndInstallingSnapshots();

        IEnumerable<string> GetInstalledModuleVersions(string snapshotPath, string moduleName, string majorVersion);

        bool IsModuleVersionInstalled(string snapshotPath, string moduleName, string version);

        string CreateNewSnapshotPath();

        string CreateInstallingSnapshot(string path);

        void PromoteInstallingSnapshotToInstalledAtomically(string path);

        void RemoveSnapshot(string path);

        void SetSnapshotCreationTimeToUtcNow(string path);

        DateTime GetSnapshotCreationTimeUtc(string path);

        void SetSnapshotAccessTimeToUtcNow(string path);

        DateTime GetSnapshotAccessTimeUtc(string path);
    }
}
