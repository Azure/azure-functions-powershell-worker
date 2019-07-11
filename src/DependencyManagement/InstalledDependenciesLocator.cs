//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System.Linq;

    internal class InstalledDependenciesLocator : IInstalledDependenciesLocator
    {
        private readonly IDependencyManagerStorage _storage;

        public InstalledDependenciesLocator(IDependencyManagerStorage storage)
        {
            _storage = storage;
        }

        public string GetPathWithAcceptableDependencyVersionsInstalled()
        {
            var snapshotPaths = _storage.GetInstalledSnapshots();
            var lastSnapshotPath = snapshotPaths.Max();
            if (lastSnapshotPath != null)
            {
                var dependencies = _storage.GetDependencies();
                if (dependencies.All(entry => IsMajorVersionInstalled(lastSnapshotPath, entry)))
                {
                    return lastSnapshotPath;
                }
            }

            return null;
        }

        private bool IsMajorVersionInstalled(string snapshotPath, DependencyManifestEntry dependency)
        {
            var installedVersions =
                _storage.GetInstalledModuleVersions(
                    snapshotPath, dependency.Name, dependency.MajorVersion);

            return installedVersions.Any();
        }
    }
}
