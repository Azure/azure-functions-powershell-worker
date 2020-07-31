//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    internal class StreamHandler
    {
        ILogger _logger;
        private ErrorRecordFormatter _errorRecordFormatter;

        public StreamHandler(ILogger logger, ErrorRecordFormatter errorRecordFormatter)
        {
            _logger = logger;
            _errorRecordFormatter = errorRecordFormatter;
        }

        public void DebugDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is DebugRecord record)
            {
                _logger.Log(isUserOnlyLog: true, LogLevel.Debug, $"DEBUG: {record.Message}");
            }
        }

        public void ErrorDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is ErrorRecord record)
            {
                ErrorLogger.Log(_logger, record, isException: false);
                _logger.Log(isUserOnlyLog: true, LogLevel.Error, $"ERROR: {_errorRecordFormatter.Format(record)}", record.Exception);
            }
        }

        public void InformationDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is InformationRecord record)
            {
                string prefix = (record.Tags.Count == 1 && record.Tags[0] == "__PipelineObject__") ? "OUTPUT:" : "INFORMATION:";
                _logger.Log(isUserOnlyLog: true, LogLevel.Information, $"{prefix} {record.MessageData}");
            }
        }

        public void ProgressDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is ProgressRecord record)
            {
                _logger.Log(isUserOnlyLog: true, LogLevel.Trace, $"PROGRESS: {record.StatusDescription}");
            }
        }

        public void VerboseDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is VerboseRecord record)
            {
                _logger.Log(isUserOnlyLog: true, LogLevel.Trace, $"VERBOSE: {record.Message}");
            }
        }

        public void WarningDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is WarningRecord record)
            {
                _logger.Log(isUserOnlyLog: true, LogLevel.Warning, $"WARNING: {record.Message}");
            }
        }
    }
}
