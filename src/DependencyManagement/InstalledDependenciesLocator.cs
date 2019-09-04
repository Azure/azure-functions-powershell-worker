//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
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
            var lastSnapshotPath = _storage.GetLatestInstalledSnapshot();
            if (lastSnapshotPath != null)
            {
                var dependencies = _storage.GetDependencies();
                if (dependencies.All(entry => IsAcceptableVersionInstalled(lastSnapshotPath, entry)))
                {
                    return lastSnapshotPath;
                }
            }

            return null;
        }

        private bool IsAcceptableVersionInstalled(string snapshotPath, DependencyManifestEntry dependency)
        {
            switch (dependency.VersionSpecificationType)
            {
                case VersionSpecificationType.ExactVersion:
                    return _storage.IsModuleVersionInstalled(
                        snapshotPath, dependency.Name, dependency.VersionSpecification);

                case VersionSpecificationType.MajorVersion:
                    return IsMajorVersionInstalled(
                        snapshotPath, dependency.Name, dependency.VersionSpecification);

                default:
                    throw new ArgumentException($"Unknown version specification type: {dependency.VersionSpecificationType}");
            }
        }

        private bool IsMajorVersionInstalled(string snapshotPath, string name, string majorVersion)
        {
            var installedVersions = _storage.GetInstalledModuleVersions(snapshotPath, name, majorVersion);
            return installedVersions.Any();
        }
    }
}
