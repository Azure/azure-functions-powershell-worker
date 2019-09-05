//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Threading;
    using Utility;
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class NewerDependencySnapshotDetector : INewerDependencySnapshotDetector, IDisposable
    {
        private readonly IDependencyManagerStorage _storage;

        private readonly IWorkerRestarter _workerRestarter;

        private Timer _timer;

        public NewerDependencySnapshotDetector(IDependencyManagerStorage storage, IWorkerRestarter workerRestarter)
        {
            _storage = storage;
            _workerRestarter = workerRestarter;
        }

        public void Start(string currentlyUsedSnapshot, ILogger logger)
        {
            var period = PowerShellWorkerConfiguration.GetTimeSpan("MDNewSnapshotCheckPeriod") ?? TimeSpan.FromHours(1);

            _timer = new Timer(
                _ => { CheckForNewerDependencySnapshot(currentlyUsedSnapshot, logger); },
                state: null,
                dueTime: period,
                period: period);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        internal void CheckForNewerDependencySnapshot(string currentlyUsedSnapshot, ILogger logger)
        {
            logger.Log(
                isUserOnlyLog: false,
                LogLevel.Trace,
                string.Format(PowerShellWorkerStrings.LookingForNewerDependencySnapshot, currentlyUsedSnapshot));

            var latestInstalledSnapshot = _storage.GetLatestInstalledSnapshot();
            if (latestInstalledSnapshot == null || string.CompareOrdinal(latestInstalledSnapshot, currentlyUsedSnapshot) <= 0)
            {
                logger.Log(
                    isUserOnlyLog: false,
                    LogLevel.Trace,
                    string.Format(PowerShellWorkerStrings.NoNewerDependencySnapshotDetected));
            }
            else
            {
                logger.Log(
                    isUserOnlyLog: false,
                    LogLevel.Trace,
                    string.Format(PowerShellWorkerStrings.NewerDependencySnapshotDetected, latestInstalledSnapshot));

                _workerRestarter.Restart(logger);
            }
        }
    }
}
