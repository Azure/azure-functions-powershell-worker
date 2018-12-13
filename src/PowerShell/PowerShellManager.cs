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

        internal void InitializeRunspace()
        {
            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            _pwsh.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}").InvokeAndClearCommands();

            // Set the PSModulePath
            Environment.SetEnvironmentVariable("PSModulePath", Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules"));
        }

        internal void InvokeProfile()
        {
            string functionAppProfileLocation = FunctionLoader.FunctionAppProfileLocation;
            if (functionAppProfileLocation == null)
            {
                _logger.Log(LogLevel.Trace, $"No 'Profile.ps1' found at: {FunctionLoader.FunctionAppRootLocation}");
                return;
            }
            
            try
            {
                // Import-Module on a .ps1 file will evaluate the script in the global scope.
                _pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                    .AddParameter("Name", functionAppProfileLocation).AddParameter("PassThru", true)
                    .AddCommand("Remove-Module")
                    .InvokeAndClearCommands();
            }
            catch (CmdletInvocationException e)
            {
                _logger.Log(
                    LogLevel.Error,
                    $"Invoking the Profile had errors. See logs for details. Profile location: {functionAppProfileLocation}",
                    e,
                    isUserLog: true);
                throw;
            }
            
            if (_pwsh.HadErrors)
            {
                _logger.Log(
                    LogLevel.Error,
                    $"Invoking the Profile had errors. See logs for details. Profile location: {functionAppProfileLocation}",
                    isUserLog: true);
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
                // If an entry point is defined, we load the script as a module and invoke the function with that name.
                // We also need to fetch the ParameterMetadata to know what to pass in as arguments.
                var parameterMetadata = RetriveParameterMetadata(functionInfo, out moduleName);
                _pwsh.AddCommand(String.IsNullOrEmpty(entryPoint) ? scriptPath : entryPoint);

                // Set arguments for each input binding parameter
                foreach (ParameterBinding binding in inputData)
                {
                    if (parameterMetadata.ContainsKey(binding.Name))
                    {
                        _pwsh.AddParameter(binding.Name, binding.Data.ToObject());
                    }
                }

                // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
                if(parameterMetadata.ContainsKey(AzFunctionInfo.TriggerMetadata))
                {
                    _logger.Log(LogLevel.Debug, "Parameter '-TriggerMetadata' found.");
                    _pwsh.AddParameter(AzFunctionInfo.TriggerMetadata, triggerMetadata);
                }

                Collection<object> pipelineItems = null;
                using (ExecutionTimer.Start(_logger, "Execution of the user's function completed."))
                {
                    pipelineItems = _pwsh.InvokeAndClearCommands<object>();
                }

                var result = _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Get-OutputBinding")
                                  .AddParameter("Purge")
                                  .InvokeAndClearCommands<Hashtable>()[0];

                if (pipelineItems != null && pipelineItems.Count > 0)
                {
                    // Log everything we received from the pipeline
                    foreach (var item in pipelineItems)
                    {
                        _logger.Log(LogLevel.Information, $"OUTPUT: {_pwsh.FormatObjectToString(item)}", isUserLog: true);
                    }
                    
                    // TODO: See GitHub issue #82. We are not settled on how to handle the Azure Functions concept of the $returns Output Binding
                    // If we would like to support Option 1 from #82, uncomment the following code:
                    // object[] items = new object[pipelineItems.Count];
                    // pipelineItems.CopyTo(items, 0);
                    // result.Add(AzFunctionInfo.DollarReturn, items);

                    // If we would like to support Option 2 from #82, uncomment this line:
                    // result.Add(AzFunctionInfo.DollarReturn, pipelineItems[pipelineItems.Count - 1]);
                }

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
        /// Helper method to prepend the FunctionApp module folder to the module path.
        /// </summary>
        internal void PrependToPSModulePath(string directory)
        {
            // Adds the passed in directory to the front of the PSModulePath using the path separator of the OS.
            string psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
            Environment.SetEnvironmentVariable("PSModulePath", $"{directory}{Path.PathSeparator}{psModulePath}");
        }

        /// <summary>
        /// Helper method to set the output binding metadata for the function that is about to run.
        /// </summary>
        internal void RegisterFunctionMetadata(AzFunctionInfo functionInfo)
        {
            var outputBindings = new ReadOnlyDictionary<string, BindingInfo>(functionInfo.OutputBindings);
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

        private Dictionary<string, ParameterMetadata> RetriveParameterMetadata(AzFunctionInfo functionInfo, out string moduleName)
        {
            moduleName = null;
            string scriptPath = functionInfo.ScriptPath;
            string entryPoint = functionInfo.EntryPoint;

            using (ExecutionTimer.Start(_logger, "Parameter metadata retrieved."))
            {
                if (String.IsNullOrEmpty(entryPoint))
                {
                    return _pwsh.AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddParameter("Name", scriptPath)
                                .InvokeAndClearCommands<ExternalScriptInfo>()[0].Parameters;
                }
                else
                {
                    moduleName = Path.GetFileNameWithoutExtension(scriptPath);
                    return _pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameter("Name", scriptPath)
                                .AddStatement()
                                .AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddParameter("Name", entryPoint)
                                .InvokeAndClearCommands<FunctionInfo>()[0].Parameters;
                }
            }
        }

        private void ResetRunspace(string moduleName)
        {
            // Reset the runspace to the Initial Session State
            _pwsh.Runspace.ResetRunspaceState();

            if (!String.IsNullOrEmpty(moduleName))
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
