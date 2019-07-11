//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    internal class DependencyManagerStorage : IDependencyManagerStorage
    {
        private readonly string _functionAppRootPath;

        private readonly string _managedDependenciesRootPath;

        public DependencyManagerStorage(string functionAppRootPath)
        {
            _functionAppRootPath = functionAppRootPath;
            _managedDependenciesRootPath = ManagedDependenciesPathDetector.GetManagedDependenciesPath(_functionAppRootPath);
        }

        public IEnumerable<DependencyManifestEntry> GetDependencies()
        {
            var dependencyManifest = new DependencyManifest(_functionAppRootPath);
            return dependencyManifest.GetEntries();
        }

        public bool SnapshotExists(string path)
        {
            return Directory.Exists(path);
        }

        public IEnumerable<string> GetInstalledSnapshots()
        {
            if (!Directory.Exists(_managedDependenciesRootPath))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateDirectories(
                _managedDependenciesRootPath,
                DependencySnapshotFolderNameTools.InstalledPattern);
        }

        public IEnumerable<string> GetInstalledAndInstallingSnapshots()
        {
            if (!Directory.Exists(_managedDependenciesRootPath))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateDirectories(_managedDependenciesRootPath);
        }

        public IEnumerable<string> GetInstalledModuleVersions(string snapshotPath, string moduleName, string majorVersion)
        {
            var modulePath = Path.Join(snapshotPath, moduleName);
            return Directory.EnumerateDirectories(modulePath, $"{majorVersion}.*");
        }

        public string CreateNewSnapshotPath()
        {
            return Path.Join(
                _managedDependenciesRootPath,
                DependencySnapshotFolderNameTools.CreateUniqueName());
        }

        public string CreateInstallingSnapshot(string path)
        {
            var installingPath = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(path);
            Directory.CreateDirectory(installingPath);
            return installingPath;
        }

        public void PromoteInstallingSnapshotToInstalledAtomically(string path)
        {
            var installingPath = DependencySnapshotFolderNameTools.ConvertInstalledToInstalling(path);
            Directory.Move(installingPath, path);
        }

        public void RemoveSnapshot(string path)
        {
            Directory.Delete(path, recursive: true);
        }

        public DateTime GetSnapshotCreationTimeUtc(string path)
        {
            return Directory.GetCreationTimeUtc(path);
        }
    }
}
