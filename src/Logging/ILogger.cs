//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    /// <summary>
    /// The abstract logger interface.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Method to log a message.
        /// </summary>
        void Log(LogLevel logLevel, string message, Exception exception = null, bool isUserLog = false);
    }
}
