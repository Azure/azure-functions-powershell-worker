//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal interface ILogger
    {
        void Log(LogLevel logLevel, string message, Exception exception = null, bool isUserOnlyLog = false);
        void SetContext(string requestId, string invocationId);
        void ResetContext();
    }
}
