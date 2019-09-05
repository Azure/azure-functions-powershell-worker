//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using Utility;
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class WorkerRestarter : IWorkerRestarter
    {
        public void Restart(ILogger logger)
        {
            // The host is supposed to interpret this exit code as the intent
            // of the Language Worker to be restarted. As a result, it can start
            // another worker process without counting these restarts as errors.
            const int IntentionalRestartExitCode = 200;

            logger.Log(
                isUserOnlyLog: false,
                LogLevel.Information,
                string.Format(PowerShellWorkerStrings.RestartingWorker, IntentionalRestartExitCode));

            Environment.Exit(IntentionalRestartExitCode);
        }
    }
}
