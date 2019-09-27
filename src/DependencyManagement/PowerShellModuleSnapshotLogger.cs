//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    using System;
    using System.IO;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class PowerShellModuleSnapshotLogger : IDependencySnapshotContentLogger
    {
        public void LogDependencySnapshotContent(string snapshotPath, ILogger logger)
        {
            try
            {
                var moduleVersionSubdirectories = PowerShellModuleSnapshotTools.GetModuleVersionSubdirectories(snapshotPath);

                foreach (var moduleVersionSubdirectory in moduleVersionSubdirectories)
                {
                    // Module version subdirectories follow the following pattern: <snapshotPath>\<module name>\<module version>
                    var moduleName = Path.GetFileName(Path.GetDirectoryName(moduleVersionSubdirectory));
                    var moduleVersion = Path.GetFileName(moduleVersionSubdirectory);
                    var snapshotName = Path.GetFileName(snapshotPath);

                    var message = string.Format(PowerShellWorkerStrings.DependencyShapshotContent, snapshotName, moduleName, moduleVersion);
                    logger.Log(isUserOnlyLog: false, LogLevel.Trace, message);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                var message = string.Format(PowerShellWorkerStrings.FailedToEnumerateDependencySnapshotContent, snapshotPath, e.Message);
                logger.Log(isUserOnlyLog: false, LogLevel.Warning, message);
            }
        }
    }
}
