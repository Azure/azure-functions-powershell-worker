//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal class BackgroundDependencySnapshotContentLogger : IBackgroundDependencySnapshotContentLogger, IDisposable
    {
        private readonly IDependencySnapshotContentLogger _snapshotContentLogger;

        private Timer _timer;

        public BackgroundDependencySnapshotContentLogger(IDependencySnapshotContentLogger snapshotContentLogger)
        {
            _snapshotContentLogger = snapshotContentLogger ?? throw new ArgumentNullException(nameof(snapshotContentLogger));
        }

        public void Start(string currentSnapshotPath, ILogger logger)
        {
            var period = PowerShellWorkerConfiguration.GetTimeSpan("MDCurrentSnapshotContentLogPeriod") ?? TimeSpan.FromDays(1);

            _timer = new Timer(
                _ => { _snapshotContentLogger.LogDependencySnapshotContent(currentSnapshotPath, logger); },
                state: null,
                dueTime: period,
                period: period);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
