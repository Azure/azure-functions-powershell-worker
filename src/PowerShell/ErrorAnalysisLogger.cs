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
            switch (errorRecord.FullyQualifiedErrorId)
            {
                case KnownErrorId.CommandNotFound:
                    LogCommandNotFoundWarning(logger, errorRecord, isException);
                    break;

                case KnownErrorId.ModuleNotFound:
                    LogModuleNotFoundWarning(logger, errorRecord, isException);
                    break;
            }
        }

        private static void LogCommandNotFoundWarning(ILogger logger, ErrorRecord errorRecord, bool isException)
        {
            var publicMessage = isException
                                    ? PowerShellWorkerStrings.CommandNotFoundException_Exception
                                    : PowerShellWorkerStrings.CommandNotFoundException_Error;

            var userMessage = string.Format(
                PowerShellWorkerStrings.CommandNotFoundUserWarning,
                errorRecord.CategoryInfo.TargetName);

            LogWarning(logger, publicMessage, userMessage);
        }

        private static void LogModuleNotFoundWarning(ILogger logger, ErrorRecord errorRecord, bool isException)
        {
            var publicMessage = isException
                                    ? PowerShellWorkerStrings.ModuleNotFound_Exception
                                    : PowerShellWorkerStrings.ModuleNotFound_Error;

            var userMessage = string.Format(
                PowerShellWorkerStrings.ModuleNotFoundUserWarning,
                errorRecord.CategoryInfo.TargetName);

            LogWarning(logger, publicMessage, userMessage);
        }

        private static void LogWarning(ILogger logger, string publicMessage, string userMessage)
        {
            logger.Log(isUserOnlyLog: false, LogLevel.Warning, publicMessage);
            logger.Log(isUserOnlyLog: true, LogLevel.Warning, userMessage);
        }

        // These error IDs is what PowerShell currently uses, even though this is not documented nor promised.
        // If this ever changes in future, the ErrorAnalysisLogger tests are supposed to catch that,
        // and these IDs or the detection logic will have to be updated.
        private static class KnownErrorId
        {
            public const string CommandNotFound = "CommandNotFoundException";
            public const string ModuleNotFound = "Modules_ModuleNotFound,Microsoft.PowerShell.Commands.ImportModuleCommand";
        }
    }
}
