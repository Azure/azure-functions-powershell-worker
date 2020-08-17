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

        public FunctionsEnvironmentReloader(ILogger logger)
            : this(logger, Environment.SetEnvironmentVariable, Directory.SetCurrentDirectory)
        {
        }

        internal FunctionsEnvironmentReloader(
            ILogger logger,
            Action<string, string> setEnvironmentVariable,
            Action<string> setCurrentDirectory)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._setEnvironmentVariable = setEnvironmentVariable;
            this._setCurrentDirectory = setCurrentDirectory;
        }

        public void ReloadEnvironment(
            IEnumerable<KeyValuePair<string, string>> environmentVariables,
            string functionAppDirectory)
        {
            foreach (var (name, value) in environmentVariables)
            {
                this._setEnvironmentVariable(name, value);
            }

            if (functionAppDirectory != null)
            {
                var setCurrentDirMessage = string.Format(PowerShellWorkerStrings.SettingCurrentDirectory, functionAppDirectory);
                _logger.Log(isUserOnlyLog: false, LogLevel.Trace, setCurrentDirMessage);
                _setCurrentDirectory(functionAppDirectory);
            }
        }
    }
}
