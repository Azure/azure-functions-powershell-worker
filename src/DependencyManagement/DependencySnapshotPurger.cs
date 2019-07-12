﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class DependencySnapshotPurger : IDependencySnapshotPurger, IDisposable
    {
        private readonly IDependencyManagerStorage _storage;

        private readonly TimeSpan _heartbeatPeriod;
        private readonly TimeSpan _oldHeartbeatAgeMargin;
        private readonly int _minNumberOfSnapshotsToKeep;

        private Timer _heartbeat;

        public DependencySnapshotPurger(
            IDependencyManagerStorage storage,
            TimeSpan? heartbeatPeriod = null,
            TimeSpan? oldHeartbeatAgeMargin = null,
            int? minNumberOfSnapshotsToKeep = null)
        {
            _storage = storage;
            _heartbeatPeriod = heartbeatPeriod ?? GetHeartbeatPeriod();
            _oldHeartbeatAgeMargin = oldHeartbeatAgeMargin ?? GetOldHeartbeatAgeMargin();
            _minNumberOfSnapshotsToKeep = minNumberOfSnapshotsToKeep ?? GetMinNumberOfSnapshotsToKeep();
        }

        /// <summary>
        /// Set the path to the snapshot currently used by the current worker.
        /// As long as there is any live worker that declared this snapshot as
        /// being in use, this snapshot should not be purged by any worker.
        /// </summary>
        public void SetCurrentlyUsedSnapshot(string path, ILogger logger)
        {
            Heartbeat(path, logger);

            _heartbeat = new Timer(
                                _ => Heartbeat(path, logger),
                                state: null,
                                dueTime: _heartbeatPeriod,
                                period: _heartbeatPeriod);
        }

        /// <summary>
        /// Remove unused snapshots.
        /// A snapshot is considered unused if it has not been accessed for at least
        /// (PSWorkerHeartbeatPeriodMinutes + PSWorkerOldSnapshotHeartbeatMarginMinutes) minutes.
        /// However, the last PSWorkerMinNumberOfSnapshotsToKeep snapshots will be kept regardless
        /// of the access time.
        /// </summary>
        public void Purge(ILogger logger)
        {
            var allSnapshotPaths = _storage.GetInstalledAndInstallingSnapshots();

            var threshold = DateTime.UtcNow - _heartbeatPeriod - _oldHeartbeatAgeMargin;

            var pathSortedByAccessTime = allSnapshotPaths
                                            .Select(path => Tuple.Create(path, GetSnapshotAccessTimeUtc(path, logger)))
                                            .OrderBy(entry => entry.Item2)
                                            .ToArray();

            for (var i = 0; i < pathSortedByAccessTime.Length - _minNumberOfSnapshotsToKeep; ++i)
            {
                var creationTime = pathSortedByAccessTime[i].Item2;
                if (creationTime > threshold)
                {
                    break;
                }

                var pathToRemove = pathSortedByAccessTime[i].Item1;

                try
                {
                    var message = string.Format(PowerShellWorkerStrings.RemovingDependenciesFolder, pathToRemove);
                    logger.Log(LogLevel.Trace, message, null, isUserLog: true);

                    _storage.RemoveSnapshot(pathToRemove);
                }
                catch (IOException e)
                {
                    var message = string.Format(PowerShellWorkerStrings.FailedToRemoveDependenciesFolder, pathToRemove, e.Message);
                    logger.Log(LogLevel.Warning, message, e, isUserLog: true);
                }
            }
        }

        public void Dispose()
        {
            _heartbeat?.Dispose();
        }

        internal void Heartbeat(string path, ILogger logger)
        {
            logger.Log(
                LogLevel.Trace,
                string.Format(
                    PowerShellWorkerStrings.UpdatingManagedDependencySnapshotHeartbeat,
                    path),
                isUserLog: true);

            if (_storage.SnapshotExists(path))
            {
                _storage.SetSnapshotAccessTimeToUtcNow(path);
            }
        }

        private DateTime GetSnapshotAccessTimeUtc(string path, ILogger logger)
        {
            try
            {
                return _storage.GetSnapshotAccessTimeUtc(path);
            }
            catch (IOException e)
            {
                var message = string.Format(PowerShellWorkerStrings.FailedToRetrieveDependenciesFolderAccessTime, path, e.Message);
                logger.Log(LogLevel.Warning, message, e, isUserLog: true);
                return DateTime.MaxValue;
            }
        }

        private static TimeSpan GetHeartbeatPeriod()
        {
            return TimeSpan.FromMinutes(
                GetEnvironmentVariableIntValue("PSWorkerHeartbeatPeriodMinutes") ?? 60);
        }

        private static TimeSpan GetOldHeartbeatAgeMargin()
        {
            return TimeSpan.FromMinutes(
                GetEnvironmentVariableIntValue("PSWorkerOldSnapshotHeartbeatMarginMinutes") ?? 90);
        }

        private static int GetMinNumberOfSnapshotsToKeep()
        {
            return GetEnvironmentVariableIntValue("PSWorkerMinNumberOfSnapshotsToKeep") ?? 1;
        }

        private static int? GetEnvironmentVariableIntValue(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value) || !int.TryParse(value, out var parsedValue))
            {
                return null;
            }

            return parsedValue;
        }
    }
}
