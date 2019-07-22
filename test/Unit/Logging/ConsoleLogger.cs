//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    internal class ConsoleLogger : ILogger
    {
        public List<string> FullLog = new List<string>();

        public void Log(LogLevel logLevel, string message, Exception exception = null, bool isUserOnlyLog = false)
        {
            var log = $"{logLevel}: {message}";
            Console.WriteLine(log);
            FullLog.Add(log);
        }

        public void SetContext(string requestId, string invocationId) {}
        public void ResetContext() {}
    }
}
