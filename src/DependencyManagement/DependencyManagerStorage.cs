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
            if (!Directory.Exists(modulePath))
            {
                return Enumerable.Empty<string>();
            }

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

        public void SetSnapshotAccessTimeToUtcNow(string path)
        {
            var markerFilePath = DependencySnapshotFolderNameTools.CreateLastAccessMarkerFilePath(path);
            if (File.Exists(markerFilePath))
            {
                File.SetLastWriteTimeUtc(markerFilePath, DateTime.UtcNow);
            }
            else
            {
                var file = File.Open(markerFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                file.Dispose();
            }

        }

        public DateTime GetSnapshotAccessTimeUtc(string path)
        {
            var heartbeatFilePath = DependencySnapshotFolderNameTools.CreateLastAccessMarkerFilePath(path);
            var heartbeatLastWrite = File.GetLastWriteTimeUtc(heartbeatFilePath);
            var snapshotCreation = GetSnapshotCreationTimeUtc(path);

            // If heartbeatLastWrite is older than snapshotCreation, this indicates that the heartbeat file
            // does not exist (as in this case File.GetLastWriteTimeUtc returns January 1, 1601 A.D.).
            // In this situation, we want to use the snapshot creation time as the closest approximation instead.
            return heartbeatLastWrite >= snapshotCreation ? heartbeatLastWrite : snapshotCreation;
        }
    }
}
