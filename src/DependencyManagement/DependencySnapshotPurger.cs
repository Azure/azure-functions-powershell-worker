//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class DependencySnapshotPurger : IDependencySnapshotPurger
    {
        private static readonly TimeSpan s_oldSnapshotAge = GetOldSnapshotAge();

        private static readonly int s_minNumberOfSnapshotsToKeep = GetMinNumberOfSnapshotsToKeep();

        private readonly IDependencyManagerStorage _storage;

        public DependencySnapshotPurger(IDependencyManagerStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Remove old unused snapshots.
        /// </summary>
        public void Purge(ILogger logger)
        {
            var allSnapshotPaths = _storage.GetInstalledAndInstallingSnapshots();

            var threshold = DateTime.UtcNow - s_oldSnapshotAge;

            var pathSortedByCreationTime = allSnapshotPaths
                                            .Select(path => Tuple.Create(path, _storage.GetSnapshotCreationTimeUtc(path)))
                                            .OrderBy(entry => entry.Item2)
                                            .ToArray();

            for (var i = 0; i < pathSortedByCreationTime.Length - s_minNumberOfSnapshotsToKeep; ++i)
            {
                var creationTime = pathSortedByCreationTime[i].Item2;
                if (creationTime > threshold)
                {
                    break;
                }

                var pathToRemove = pathSortedByCreationTime[i].Item1;

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

        private static TimeSpan GetOldSnapshotAge()
        {
            var value = Environment.GetEnvironmentVariable("PSWorkerOldSnapshotAgeMinutes");
            if (string.IsNullOrEmpty(value) || !int.TryParse(value, out var parsedValue))
            {
                return TimeSpan.FromDays(7);
            }

            return TimeSpan.FromMinutes(parsedValue);
        }

        private static int GetMinNumberOfSnapshotsToKeep()
        {
            var value = Environment.GetEnvironmentVariable("PSWorkerMinNumberOfSnapshotsToKeep");
            if (string.IsNullOrEmpty(value) || !int.TryParse(value, out var parsedValue))
            {
                return 2;
            }

            return parsedValue;
        }
    }
}
