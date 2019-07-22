//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1573 // "Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)"

using System;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal interface ILogger
    {
        /// <param name="isUserOnlyLog">
        /// isUserOnlyLog must be true when logging data that belongs to the user and may contain
        /// secrets or PII, such as PowerShell streams produced by the Function code, console output
        /// of external commands, etc.
        /// Messages sent with isUserOnlyLog == true will be visible to the Function user only.
        /// 
        /// isUserOnlyLog must be false when logging internal telemetry messages free of secrets and PII
        /// for diagnostic and statistical purposes.
        /// Messages sent with isUserOnlyLog == false will be visible to both the user and the service engineers.
        ///
        /// Regardless of the isUserOnlyLog value, all these messages will be available at the
        /// usual locations (for example, the current Azure Portal implementation will show them on
        /// the Logs tab under the Function source code, on the Monitor page under the Function,
        /// and in the Application Insights logs). The only difference is that the messages written
        /// with isUserOnlyLog == true will be available to the user, but not to the service engineers.
        /// The messages written with isUserOnlyLog == false will also be sent to a location like an
        /// internal log storage available to the service engineers.
        ///
        /// It is important to understand and strictly maintain this separation.
        /// When in doubt, use isUserOnlyLog == true.
        /// </param>
        void Log(bool isUserOnlyLog, LogLevel logLevel, string message, Exception exception = null);
        void SetContext(string requestId, string invocationId);
        void ResetContext();
    }
}
