//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    internal class StreamHandler
    {
        ILogger _logger;

        public StreamHandler(ILogger logger)
        {
            _logger = logger;
        }

        public void DebugDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is DebugRecord record)
            {
                _logger.Log(LogLevel.Debug, $"DEBUG: {record.Message}", isUserLog: true);
            }
        }

        public void ErrorDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is ErrorRecord record)
            {
                _logger.Log(LogLevel.Error, $"ERROR: {record.Exception.Message}", record.Exception, isUserLog: true);
            }
        }

        public void InformationDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is InformationRecord record)
            {
                string prefix = (record.Tags.Count == 1 && record.Tags[0] == WriteFunctionOutputCommand.OutputTag) ? "OUTPUT:" : "INFORMATION:";
                _logger.Log(LogLevel.Information, $"{prefix} {record.MessageData}", isUserLog: true);
            }
        }

        public void ProgressDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is ProgressRecord record)
            {
                _logger.Log(LogLevel.Trace, $"PROGRESS: {record.StatusDescription}", isUserLog: true);
            }
        }

        public void VerboseDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is VerboseRecord record)
            {
                _logger.Log(LogLevel.Trace, $"VERBOSE: {record.Message}", isUserLog: true);
            }
        }

        public void WarningDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is WarningRecord record)
            {
                _logger.Log(LogLevel.Warning, $"WARNING: {record.Message}", isUserLog: true);
            }
        }
    }
}
