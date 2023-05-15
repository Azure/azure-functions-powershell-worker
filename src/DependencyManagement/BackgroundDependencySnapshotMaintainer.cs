//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;
    using WebJobs.Script.Grpc.Messages;

    internal class BackgroundDependencySnapshotMaintainer : IBackgroundDependencySnapshotMaintainer, IDisposable
    {
        internal TimeSpan MinBackgroundUpgradePeriod { get; set; } =
            PowerShellWorkerConfiguration.GetTimeSpan("MDMinBackgroundUpgradePeriod") ?? TimeSpan.FromDays(1);

        private TimeSpan MaxBackgroundUpgradePeriod { get; } =
            PowerShellWorkerConfiguration.GetTimeSpan("MDMaxBackgroundUpgradePeriod") ?? TimeSpan.FromDays(7);

        private bool EnableAutomaticUpgrades { get; } =
            PowerShellWorkerConfiguration.GetBoolean("MDEnableAutomaticUpgrades") ?? false;

        private readonly IDependencyManagerStorage _storage;
        private readonly IDependencySnapshotInstaller _installer;
        private readonly IDependencySnapshotPurger _purger;
        private DependencyManifestEntry[] _dependencyManifest;
        private Timer _installAndPurgeTimer;

        public BackgroundDependencySnapshotMaintainer(
            IDependencyManagerStorage storage,
            IDependencySnapshotInstaller installer,
            IDependencySnapshotPurger purger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
            _purger = purger ?? throw new ArgumentNullException(nameof(purger));
        }

        public void Start(string currentSnapshotPath, DependencyManifestEntry[] dependencyManifest, ILogger logger)
        {
            _dependencyManifest = dependencyManifest ?? throw new ArgumentNullException(nameof(dependencyManifest));

            _purger.SetCurrentlyUsedSnapshot(currentSnapshotPath, logger);

            if (!EnableAutomaticUpgrades)
            {
                logger.Log(
                    isUserOnlyLog: false,
                    RpcLog.Types.Level.Warning,
                    PowerShellWorkerStrings.AutomaticUpgradesAreDisabled);

                return;
            }

            _installAndPurgeTimer = new Timer(
                                            _ => InstallAndPurgeSnapshots(PowerShell.Create, logger),
                                            state: null,
                                            dueTime: MaxBackgroundUpgradePeriod,
                                            period: MaxBackgroundUpgradePeriod);
        }

        /// <summary>
        /// Returns the path for the new dependencies snapshot.
        /// </summary>
        public string InstallAndPurgeSnapshots(Func<PowerShell> pwshFactory, ILogger logger)
        {
            try
            {
                // Purge before installing a new snapshot, as we may be able to free some space.
                _purger.Purge(logger);

                if (IsAnyInstallationStartedRecently())
                {
                    return null;
                }

                var nextSnapshotPath = _storage.CreateNewSnapshotPath();

                using (var pwsh = pwshFactory())
                {
                    _installer.InstallSnapshot(
                        _dependencyManifest,
                        nextSnapshotPath,
                        pwsh,
                        // Background dependency upgrades are optional because the current
                        // worker already has a good enough snapshot, and nothing depends on
                        // the new snapshot yet, so installation failures will not affect
                        // function invocations.
                        DependencySnapshotInstallationMode.Optional,
                        logger);
                }

                // Now that a new snapshot has been installed, there is a chance an old snapshot can be purged.
                _purger.Purge(logger);

                return nextSnapshotPath;
            }
            catch (Exception e)
            {
                logger.Log(
                    isUserOnlyLog: false,
                    RpcLog.Types.Level.Warning,
                    string.Format(PowerShellWorkerStrings.DependenciesUpgradeSkippedMessage, e.Message));

                return null;
            }
        }

        public void Dispose()
        {
            (_purger as IDisposable)?.Dispose();
            _installAndPurgeTimer?.Dispose();
        }

        private bool IsAnyInstallationStartedRecently()
        {
            var threshold = DateTime.UtcNow - MinBackgroundUpgradePeriod;

            return _storage
                .GetInstalledAndInstallingSnapshots()
                .Select(path => _storage.GetSnapshotCreationTimeUtc(path))
                .Any(creationTime => creationTime > threshold);
        }
    }
}
