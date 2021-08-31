//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;
    using static Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types;

    internal class InstalledDependenciesLocator : IInstalledDependenciesLocator
    {
        private readonly IDependencyManagerStorage _storage;

        private readonly ILogger _logger;

        public InstalledDependenciesLocator(IDependencyManagerStorage storage, ILogger logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetPathWithAcceptableDependencyVersionsInstalled()
        {
            var lastSnapshotPath = _storage.GetLatestInstalledSnapshot();
            if (lastSnapshotPath == null)
            {
                _logger.Log(isUserOnlyLog: false, Level.Information, string.Format(PowerShellWorkerStrings.NoInstalledDependencySnapshot, lastSnapshotPath));
                return null;
            }

            _logger.Log(isUserOnlyLog: false, Level.Information, string.Format(PowerShellWorkerStrings.LastInstalledDependencySnapshotFound, lastSnapshotPath));

            if (_storage.IsEquivalentDependencyManifest(lastSnapshotPath))
            {
                _logger.Log(isUserOnlyLog: false, Level.Information, string.Format(PowerShellWorkerStrings.EquivalentDependencySnapshotManifest, lastSnapshotPath));
                return lastSnapshotPath;
            }

            var dependencies = _storage.GetDependencies();
            if (dependencies.All(entry => IsAcceptableVersionInstalled(lastSnapshotPath, entry)))
            {
                _logger.Log(isUserOnlyLog: false, Level.Information, string.Format(PowerShellWorkerStrings.DependencySnapshotContainsAcceptableModuleVersions, lastSnapshotPath));
                return lastSnapshotPath;
            }

            _logger.Log(isUserOnlyLog: false, Level.Information, string.Format(PowerShellWorkerStrings.DependencySnapshotDoesNotContainAcceptableModuleVersions, lastSnapshotPath));
            return null;
        }

        private bool IsAcceptableVersionInstalled(string snapshotPath, DependencyManifestEntry dependency)
        {
            switch (dependency.VersionSpecificationType)
            {
                case VersionSpecificationType.ExactVersion:
                    return _storage.IsModuleVersionInstalled(
                                snapshotPath, dependency.Name, dependency.VersionSpecification)
                        || _storage.IsModuleVersionInstalled(
                                snapshotPath, dependency.Name, StripVersionPostfix(dependency.VersionSpecification));

                case VersionSpecificationType.MajorVersion:
                    return IsMajorVersionInstalled(
                        snapshotPath, dependency.Name, dependency.VersionSpecification);

                default:
                    throw new ArgumentException($"Unknown version specification type: {dependency.VersionSpecificationType}");
            }
        }

        private string StripVersionPostfix(string versionSpecification)
        {
            const char PostfixSeparator = '-';
            var separatorPos = versionSpecification.IndexOf(PostfixSeparator);
            return separatorPos == -1 ? versionSpecification : versionSpecification.Substring(0, separatorPos);
        }

        private bool IsMajorVersionInstalled(string snapshotPath, string name, string majorVersion)
        {
            var installedVersions = _storage.GetInstalledModuleVersions(snapshotPath, name, majorVersion);
            return installedVersions.Any();
        }
    }
}
