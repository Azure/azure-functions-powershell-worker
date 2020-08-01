//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    internal class ErrorAnalysisLogger
    {
        public static void Log(ILogger logger, ErrorRecord errorRecord, bool isException)
        {
            if (errorRecord.FullyQualifiedErrorId == KnownErrorId.CommandNotFound)
            {
                var publicMessage = isException
                                        ? PowerShellWorkerStrings.CommandNotFoundException_Exception
                                        : PowerShellWorkerStrings.CommandNotFoundException_Error;

                logger.Log(isUserOnlyLog: false, LogLevel.Warning, publicMessage);

                var userMessage = string.Format(
                    PowerShellWorkerStrings.CommandNotFoundUserWarning,
                    errorRecord.CategoryInfo.TargetName);

                logger.Log(isUserOnlyLog: true, LogLevel.Warning, userMessage);
            }
            else if (errorRecord.FullyQualifiedErrorId == KnownErrorId.ModuleNotFound)
            {
                var publicMessage = isException
                                        ? PowerShellWorkerStrings.ModuleNotFound_Exception
                                        : PowerShellWorkerStrings.ModuleNotFound_Error;

                logger.Log(isUserOnlyLog: false, LogLevel.Warning, publicMessage);

                var userMessage = string.Format(
                    PowerShellWorkerStrings.ModuleNotFoundUserWarning,
                    errorRecord.CategoryInfo.TargetName);

                logger.Log(isUserOnlyLog: true, LogLevel.Warning, userMessage);
            }
        }

        private static class KnownErrorId
        {
            public const string CommandNotFound = "CommandNotFoundException";
            public const string ModuleNotFound = "Modules_ModuleNotFound,Microsoft.PowerShell.Commands.ImportModuleCommand";
        }
    }
}
