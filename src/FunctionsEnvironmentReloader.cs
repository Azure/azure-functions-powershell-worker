//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class FunctionsEnvironmentReloader
    {
        private readonly ILogger _logger;
        private readonly Action<string, string> _setEnvironmentVariable;
        private readonly Action<string> _setCurrentDirectory;
        private readonly Action _clearTimeZoneCache;

        public FunctionsEnvironmentReloader(ILogger logger)
            : this(logger, Environment.SetEnvironmentVariable, Directory.SetCurrentDirectory, TimeZoneInfo.ClearCachedData)
        {
        }

        internal FunctionsEnvironmentReloader(
            ILogger logger,
            Action<string, string> setEnvironmentVariable,
            Action<string> setCurrentDirectory,
            Action clearTimeZoneCache)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._setEnvironmentVariable = setEnvironmentVariable;
            this._setCurrentDirectory = setCurrentDirectory;
            this._clearTimeZoneCache = clearTimeZoneCache;
        }

        public void ReloadEnvironment(
            IEnumerable<KeyValuePair<string, string>> environmentVariables,
            string functionAppDirectory)
        {
            foreach (var (name, value) in environmentVariables)
            {
                this._setEnvironmentVariable(name, value);
            }

            // Clear cached timezone data to ensure timezone-related commands
            // (e.g., Get-TimeZone) respect the updated TZ environment variable
            this._clearTimeZoneCache();

            if (functionAppDirectory != null)
            {
                var setCurrentDirMessage = string.Format(PowerShellWorkerStrings.SettingCurrentDirectory, functionAppDirectory);
                _logger.Log(isUserOnlyLog: false, LogLevel.Trace, setCurrentDirMessage);
                _setCurrentDirectory(functionAppDirectory);
            }
        }
    }
}
