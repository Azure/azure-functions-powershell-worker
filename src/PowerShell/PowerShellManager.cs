//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Management.Automation.Runspaces;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    internal class PowerShellManager
    {
        private readonly ILogger _logger;
        private readonly PowerShell _pwsh;

        internal PowerShellManager(ILogger logger)
        {
            var initialSessionState = InitialSessionState.CreateDefault();

            // Setting the execution policy on macOS and Linux throws an exception so only update it on Windows
            if(Platform.IsWindows)
            {
                // This sets the execution policy on Windows to Unrestricted which is required to run the user's function scripts on
                // Windows client versions. This is needed if a user is testing their function locally with the func CLI
                initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
            }
            _pwsh = PowerShell.Create(initialSessionState);
            _logger = logger;

            // Setup Stream event listeners
            var streamHandler = new StreamHandler(logger);
            _pwsh.Streams.Debug.DataAdding += streamHandler.DebugDataAdding;
            _pwsh.Streams.Error.DataAdding += streamHandler.ErrorDataAdding;
            _pwsh.Streams.Information.DataAdding += streamHandler.InformationDataAdding;
            _pwsh.Streams.Progress.DataAdding += streamHandler.ProgressDataAdding;
            _pwsh.Streams.Verbose.DataAdding += streamHandler.VerboseDataAdding;
            _pwsh.Streams.Warning.DataAdding += streamHandler.WarningDataAdding;
        }

        /// <summary>
        /// This method performs the one-time initialization at the worker process level.
        /// </summary>
        internal void PerformWorkerLevelInitialization()
        {
            // Set the type accelerators for 'HttpResponseContext' and 'HttpResponseContext'.
            // We probably will expose more public types from the worker in future for the interop between worker and the 'PowerShellWorker' module.
            // But it's most likely only 'HttpResponseContext' and 'HttpResponseContext' are supposed to be used directly by users, so we only add
            // type accelerators for these two explicitly.
            var accelerator = typeof(PSObject).Assembly.GetType("System.Management.Automation.TypeAccelerators");
            var addMethod = accelerator.GetMethod("Add", new Type[] { typeof(string), typeof(Type) });
            addMethod.Invoke(null, new object[] { "HttpResponseContext", typeof(HttpResponseContext) });
            addMethod.Invoke(null, new object[] { "HttpRequestContext", typeof(HttpRequestContext) });

            // Set the PSModulePath
            var workerModulesPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            Environment.SetEnvironmentVariable("PSModulePath", $"{FunctionLoader.FunctionAppModulesPath}{Path.PathSeparator}{workerModulesPath}");
        }

        /// <summary>
        /// This method performs initialization that has to be done for each Runspace, e.g. profile.ps1.
        /// </summary>
        internal void PerformRunspaceLevelInitialization()
        {
            Exception exception = null;
            string profilePath = FunctionLoader.FunctionAppProfilePath;
            if (profilePath == null)
            {
                _logger.Log(LogLevel.Trace, $"No 'profile.ps1' is found at the FunctionApp root folder: {FunctionLoader.FunctionAppRootPath}");
                return;
            }

            try
            {
                // Import-Module on a .ps1 file will evaluate the script in the global scope.
                _pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                     .AddParameter("Name", profilePath).AddParameter("PassThru", true)
                     .AddCommand("Microsoft.PowerShell.Core\\Remove-Module")
                     .AddParameter("Force", true).AddParameter("ErrorAction", "SilentlyContinue")
                     .InvokeAndClearCommands();
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                if (_pwsh.HadErrors)
                {
                    string errorMsg = $"Fail to run profile.ps1. See logs for detailed errors. Profile location: {profilePath}";
                    _logger.Log(LogLevel.Error, errorMsg, exception, isUserLog: true);
                }
            }
        }

        /// <summary>
        /// Execution a function fired by a trigger or an activity function scheduled by an orchestration.
        /// </summary>
        internal Hashtable InvokeFunction(
            AzFunctionInfo functionInfo,
            Hashtable triggerMetadata,
            IList<ParameterBinding> inputData)
        {
            string scriptPath = functionInfo.ScriptPath;
            string entryPoint = functionInfo.EntryPoint;
            string moduleName = null;

            try
            {
                bool hasEntryPoint = !string.IsNullOrEmpty(entryPoint);
                if (hasEntryPoint)
                {
                    // If an entry point is defined, we import the script module.
                    moduleName = Path.GetFileNameWithoutExtension(scriptPath);
                    _pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameter("Name", scriptPath)
                         .InvokeAndClearCommands();
                }

                _pwsh.AddCommand(hasEntryPoint ? entryPoint : scriptPath);

                // Set arguments for each input binding parameter
                foreach (ParameterBinding binding in inputData)
                {
                    if (functionInfo.FuncParameters.Contains(binding.Name))
                    {
                        _pwsh.AddParameter(binding.Name, binding.Data.ToObject());
                    }
                }

                // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
                if(functionInfo.FuncParameters.Contains(AzFunctionInfo.TriggerMetadata))
                {
                    _logger.Log(LogLevel.Debug, "Parameter '-TriggerMetadata' found.");
                    _pwsh.AddParameter(AzFunctionInfo.TriggerMetadata, triggerMetadata);
                }

                Collection<object> pipelineItems = null;
                using (ExecutionTimer.Start(_logger, "Execution of the user's function completed."))
                {
                    pipelineItems = _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Trace-PipelineObject")
                                         .InvokeAndClearCommands<object>();
                }

                var result = _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Get-OutputBinding")
                                  .AddParameter("Purge", true)
                                  .InvokeAndClearCommands<Hashtable>()[0];

                /*
                 * TODO: See GitHub issue #82. We are not settled on how to handle the Azure Functions concept of the $returns Output Binding
                if (pipelineItems != null && pipelineItems.Count > 0)
                {
                    // If we would like to support Option 1 from #82, use the following 3 lines of code:                    
                    object[] items = new object[pipelineItems.Count];
                    pipelineItems.CopyTo(items, 0);
                    result.Add(AzFunctionInfo.DollarReturn, items);

                    // If we would like to support Option 2 from #82, use this line:
                    result.Add(AzFunctionInfo.DollarReturn, pipelineItems[pipelineItems.Count - 1]);
                }
                */

                return result;
            }
            finally
            {
                ResetRunspace(moduleName);
            }
        }

        /// <summary>
        /// Helper method to convert the result returned from a function to JSON.
        /// </summary>
        internal string ConvertToJson(object fromObj)
        {
            return _pwsh.AddCommand("Microsoft.PowerShell.Utility\\ConvertTo-Json")
                        .AddParameter("InputObject", fromObj)
                        .AddParameter("Depth", 3)
                        .AddParameter("Compress", true)
                        .InvokeAndClearCommands<string>()[0];
        }

        /// <summary>
        /// Helper method to set the output binding metadata for the function that is about to run.
        /// </summary>
        internal void RegisterFunctionMetadata(AzFunctionInfo functionInfo)
        {
            var outputBindings = functionInfo.OutputBindings;
            FunctionMetadata.OutputBindingCache.AddOrUpdate(_pwsh.Runspace.InstanceId,
                                                            outputBindings,
                                                            (key, value) => outputBindings);
        }

        /// <summary>
        /// Helper method to clear the output binding metadata for the function that has done running.
        /// </summary>
        internal void UnregisterFunctionMetadata()
        {
            FunctionMetadata.OutputBindingCache.TryRemove(_pwsh.Runspace.InstanceId, out _);
        }

        private void ResetRunspace(string moduleName)
        {
            // Reset the runspace to the Initial Session State
            _pwsh.Runspace.ResetRunspaceState();

            if (!string.IsNullOrEmpty(moduleName))
            {
                // If the function had an entry point, this will remove the module that was loaded
                _pwsh.AddCommand("Microsoft.PowerShell.Core\\Remove-Module")
                    .AddParameter("Name", moduleName)
                    .AddParameter("Force", true)
                    .AddParameter("ErrorAction", "SilentlyContinue")
                    .InvokeAndClearCommands();
            }
        }
    }
}
