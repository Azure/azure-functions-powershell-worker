//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    internal class ErrorLogger
    {
        public static void Log(ILogger logger, ErrorRecord errorRecord, bool isException)
        {
            var publicMessage = isException
                                    ? PowerShellWorkerStrings.CommandNotFoundException_Exception
                                    : PowerShellWorkerStrings.CommandNotFoundException_Error;

            logger.Log(isUserOnlyLog: false, LogLevel.Warning, publicMessage);

            var userMessage = string.Format(
                PowerShellWorkerStrings.CommandNotFoundUserWarning,
                (errorRecord.Exception as CommandNotFoundException)?.CommandName);

            logger.Log(isUserOnlyLog: true, LogLevel.Warning, userMessage);
        }
    }
}
