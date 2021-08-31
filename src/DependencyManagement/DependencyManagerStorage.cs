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
            return GetAppDependencyManifest().GetEntries();
        }

        public bool SnapshotExists(string path)
        {
            return Directory.Exists(path);
        }

        public string GetLatestInstalledSnapshot()
        {
            return GetInstalledSnapshots().Max();
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

        public bool IsModuleVersionInstalled(string snapshotPath, string moduleName, string version)
        {
            var moduleVersionPath = Path.Join(snapshotPath, moduleName, version);
            return Directory.Exists(moduleVersionPath);
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

        public void SetSnapshotCreationTimeToUtcNow(string path)
        {
            Directory.SetCreationTimeUtc(path, DateTime.UtcNow);
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

        public void PreserveDependencyManifest(string path)
        {
            var source = GetAppDependencyManifest().GetPath();
            var destination = Path.Join(path, Path.GetFileName(source));
            File.Copy(source, destination, overwrite: true);
        }

        public bool IsEquivalentDependencyManifest(string path)
        {
            var source = GetAppDependencyManifest().GetPath();
            if (!File.Exists(source))
            {
                return false;
            }

            var destination = Path.Join(path, Path.GetFileName(source));
            if (!File.Exists(destination))
            {
                return false;
            }
            
            return File.ReadAllText(source) == File.ReadAllText(destination);
        }

        private IEnumerable<string> GetInstalledSnapshots()
        {
            if (!Directory.Exists(_managedDependenciesRootPath))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateDirectories(
                _managedDependenciesRootPath,
                DependencySnapshotFolderNameTools.InstalledPattern);
        }

        private DependencyManifest GetAppDependencyManifest()
        {
            return new DependencyManifest(_functionAppRootPath);
        }
    }
}
