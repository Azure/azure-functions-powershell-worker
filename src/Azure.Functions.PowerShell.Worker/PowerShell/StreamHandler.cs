//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    public class StreamHandler
    {
        RpcLogger _logger;

        public StreamHandler(RpcLogger logger)
        {
            _logger = logger;
        }

        public void DebugDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is DebugRecord record)
            {
                _logger.LogDebug($"DEBUG: {record.Message}");
            }
        }

        public void ErrorDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is ErrorRecord record)
            {
                _logger.LogError(record.Exception, $"ERROR: {record.Exception.Message}");
            }
        }

        public void InformationDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is InformationRecord record)
            {
                _logger.LogInformation($"INFORMATION: {record.MessageData}");
            }
        }

        public void ProgressDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is ProgressRecord record)
            {
                _logger.LogTrace($"PROGRESS: {record.StatusDescription}");
            }
        }

        public void VerboseDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is VerboseRecord record)
            {
                _logger.LogTrace($"VERBOSE: {record.Message}");
            }
        }

        public void WarningDataAdding(object sender, DataAddingEventArgs e)
        {
            if(e.ItemAdded is WarningRecord record)
            {
                _logger.LogWarning($"WARNING: {record.Message}");
            }
        }
    }
}