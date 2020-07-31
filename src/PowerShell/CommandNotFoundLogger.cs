//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    internal class CommandNotFoundLogger
    {
        public static void Log(ILogger logger, bool isException)
        {
            var message = isException
                            ? PowerShellWorkerStrings.CommandNotFoundException_Exception
                            : PowerShellWorkerStrings.CommandNotFoundException_Error;

            logger.Log(isUserOnlyLog: false, LogLevel.Warning, message);
        }
    }
}
