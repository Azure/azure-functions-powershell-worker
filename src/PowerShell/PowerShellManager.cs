//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security;

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

        internal void AuthenticateToAzure()
        {
            // Check if Az.Profile is available
            Collection<PSModuleInfo> azProfile = _pwsh.AddCommand("Get-Module")
                .AddParameter("ListAvailable")
                .AddParameter("Name", "Az.Profile")
                .InvokeAndClearCommands<PSModuleInfo>();

            if (azProfile.Count == 0)
            {
                _logger.Log(LogLevel.Trace, "Required module to automatically authenticate with Azure `Az.Profile` was not found in the PSModulePath.");
                return;
            }

            // Try to authenticate to Azure using MSI
            string msiSecret = Environment.GetEnvironmentVariable("MSI_SECRET");
            string msiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
            string accountId = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");

            if (!string.IsNullOrEmpty(msiSecret) &&
                !string.IsNullOrEmpty(msiEndpoint) &&
                !string.IsNullOrEmpty(accountId))
            {
                // NOTE: There is a limitation in Azure PowerShell that prevents us from using the parameter set:
                // Connect-AzAccount -MSI or Connect-AzAccount -Identity
                // see this GitHub issue https://github.com/Azure/azure-powershell/issues/7876
                // As a workaround, we can all an API endpoint on the MSI_ENDPOINT to get an AccessToken and use that to authenticate
                Collection<PSObject> response = _pwsh.AddCommand("Microsoft.PowerShell.Utility\\Invoke-RestMethod")
                    .AddParameter("Method", "Get")
                    .AddParameter("Headers", new Hashtable {{ "Secret", msiSecret }})
                    .AddParameter("Uri", $"{msiEndpoint}?resource=https://management.azure.com&api-version=2017-09-01")
                    .InvokeAndClearCommands<PSObject>();

                if(_pwsh.HadErrors) 
                {
                    _logger.Log(LogLevel.Warning, "Failed to Authenticate to Azure via MSI. Check the logs for the errors generated.");
                }
                else
                {
                    // We have successfully authenticated to Azure so we can return out.
                    using (ExecutionTimer.Start(_logger, "Authentication to Azure"))
                    {
                        _pwsh.AddCommand("Az.Profile\\Connect-AzAccount")
                            .AddParameter("AccessToken", response[0].Properties["access_token"].Value)
                            .AddParameter("AccountId", accountId)
                            .InvokeAndClearCommands();

                        if(_pwsh.HadErrors)
                        {
                            _logger.Log(LogLevel.Warning, "Failed to Authenticate to Azure. Check the logs for the errors generated.");
                        }
                        else
                        {
                            // We've successfully authenticated to Azure so we can return
                            return;
                        }
                    }
                }
            }
            else
            {
                _logger.Log(LogLevel.Trace, "Skip authentication to Azure via MSI. Environment variables for authenticating to Azure are not present.");
            }
        }

        internal void InitializeRunspace()
        {
            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            _pwsh.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}").InvokeAndClearCommands();

            // Set the PSModulePath
            Environment.SetEnvironmentVariable("PSModulePath", Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules"));
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
                    pipelineItems = _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Write-FunctionOutput")
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
