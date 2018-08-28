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

        public void DebugDataAdded(object data, DataAddedEventArgs e)
        {
            if(data is PSDataCollection<DebugRecord> records)
            {
                _logger.LogDebug($"DEBUG: {records[e.Index].Message}");
            }
        }

        public void ErrorDataAdded(object data, DataAddedEventArgs e)
        {
            if(data is PSDataCollection<ErrorRecord> records)
            {
                _logger.LogError(records[e.Index].Exception, $"ERROR: {records[e.Index].Exception.Message}");
            }
        }

        public void InformationDataAdded(object data, DataAddedEventArgs e)
        {
            if(data is PSDataCollection<InformationRecord> records)
            {
                _logger.LogInformation($"INFORMATION: {records[e.Index].MessageData}");
            }
        }

        public void ProgressDataAdded(object data, DataAddedEventArgs e)
        {
            if(data is PSDataCollection<ProgressRecord> records)
            {
                _logger.LogTrace($"PROGRESS: {records[e.Index].StatusDescription}");
            }
        }

        public void VerboseDataAdded(object data, DataAddedEventArgs e)
        {
            if(data is PSDataCollection<VerboseRecord> records)
            {
                _logger.LogTrace($"VERBOSE: {records[e.Index].Message}");
            }
        }

        public void WarningDataAdded(object data, DataAddedEventArgs e)
        {
            if(data is PSDataCollection<WarningRecord> records)
            {
                _logger.LogWarning($"WARNING: {records[e.Index].Message}");
            }
        }
    }
}