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
using Microsoft.Extensions.Logging;
using System.Management.Automation.Runspaces;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    internal class PowerShellManager
    {
        readonly static string s_TriggerMetadataParameterName = "TriggerMetadata";

        RpcLogger _logger;
        PowerShell _pwsh;

        internal PowerShellManager(RpcLogger logger)
        {
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
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
            
            // Prepend the path to the internal Modules folder to the PSModulePath
            var modulePath = Environment.GetEnvironmentVariable("PSModulePath");
            var additionalPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            Environment.SetEnvironmentVariable("PSModulePath", $"{additionalPath}{Path.PathSeparator}{modulePath}");
        }

        internal Hashtable InvokeFunction(
            string scriptPath,
            string entryPoint,
            Hashtable triggerMetadata,
            IList<ParameterBinding> inputData)
        {
            try
            {
                Dictionary<string, ParameterMetadata> parameterMetadata;

                // We need to take into account if the user has an entry point.
                // If it does, we invoke the command of that name. We also need to fetch
                // the ParameterMetadata so that we can tell whether or not the user is asking
                // for the $TriggerMetadata
                using (ExecutionTimer.Start(_logger, "Parameter metadata retrieved."))
                {
                    if (entryPoint != "")
                    {
                        parameterMetadata = _pwsh
                            .AddScript($@". {scriptPath}")
                            .AddStatement()
                            .AddCommand("Get-Command", useLocalScope: true).AddParameter("Name", entryPoint)
                            .InvokeAndClearCommands<FunctionInfo>()[0].Parameters;

                        _pwsh.AddCommand(entryPoint, useLocalScope: true);

                    }
                    else
                    {
                        parameterMetadata = _pwsh.AddCommand("Get-Command", useLocalScope: true).AddParameter("Name", scriptPath)
                            .InvokeAndClearCommands<ExternalScriptInfo>()[0].Parameters;
                        _pwsh.AddCommand(scriptPath, useLocalScope: true);
                    }
                }

                // Sets the variables for each input binding
                foreach (ParameterBinding binding in inputData)
                {
                    _pwsh.AddParameter(binding.Name, binding.Data.ToObject());
                }

                // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
                if(parameterMetadata.ContainsKey(s_TriggerMetadataParameterName))
                {
                    _pwsh.AddParameter(s_TriggerMetadataParameterName, triggerMetadata);
                    _logger.LogDebug($"TriggerMetadata found. Value:{Environment.NewLine}{triggerMetadata.ToString()}");
                }

                PSObject returnObject = null;
                using (ExecutionTimer.Start(_logger, "Execution of the user's function completed."))
                {
                    // Log everything we received from the pipeline and set the last one to be the ReturnObject
                    Collection<PSObject> pipelineItems = _pwsh.InvokeAndClearCommands<PSObject>();
                    foreach (var psobject in pipelineItems)
                    {
                        _logger.LogInformation($"FROM FUNCTION: {psobject.ToString()}");
                    }
                    
                    returnObject = pipelineItems[pipelineItems.Count - 1];
                }
                
                var result = _pwsh.AddCommand("Azure.Functions.PowerShell.Worker.Module\\Get-OutputBinding", useLocalScope: true)
                    .AddParameter("Purge")
                    .InvokeAndClearCommands<Hashtable>()[0];

                if(returnObject != null)
                {
                    result.Add("$return", returnObject);
                }
                return result;
            }
            finally
            {
                ResetRunspace();
            }
        }

        private void ResetRunspace()
        {
            // Reset the runspace to the Initial Session State
            _pwsh.Runspace.ResetRunspaceState();
        }
    }
}
