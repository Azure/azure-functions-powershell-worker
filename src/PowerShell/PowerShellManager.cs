//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using System.Management.Automation.Runspaces;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;
    using System.Reflection;

    internal class PowerShellManager
    {
        readonly static string s_TriggerMetadataParameterName = "TriggerMetadata";
        readonly static bool s_UseLocalScope = true;

        RpcLogger _logger;
        PowerShell _pwsh;

        PowerShellManager(PowerShell pwsh, RpcLogger logger)
        {
            _pwsh = pwsh;
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

        public static PowerShellManager Create(RpcLogger logger)
        {
            // Set up initial session state: set execution policy, import helper module, and using namespace
            var initialSessionState = InitialSessionState.CreateDefault();
            if(Platform.IsWindows)
            {
                initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
            }
            var pwsh = PowerShell.Create(initialSessionState);

            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            // and also import the Azure Functions binding helper module
            string modulePath = System.IO.Path.Join(
                AppDomain.CurrentDomain.BaseDirectory,
                "Azure.Functions.PowerShell.Worker.Module",
                "Azure.Functions.PowerShell.Worker.Module.psd1");
            pwsh.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}")
                .AddStatement()
                .AddCommand("Import-Module")
                .AddParameter("Name", modulePath)
                .AddParameter("Scope", "Global")
                .InvokeAndClearCommands();

            return new PowerShellManager(pwsh, logger);
        }

        public Hashtable InvokeFunction(
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
                            .AddScript($@". {scriptPath}", s_UseLocalScope)
                            .AddStatement()
                            .AddCommand("Get-Command", s_UseLocalScope).AddParameter("Name", entryPoint)
                            .InvokeAndClearCommands<FunctionInfo>()[0].Parameters;

                        _pwsh
                            .AddScript($@". {scriptPath}", s_UseLocalScope)
                            .AddStatement()
                            .AddCommand(entryPoint, s_UseLocalScope);

                    }
                    else
                    {
                        parameterMetadata = _pwsh.AddCommand("Get-Command", s_UseLocalScope).AddParameter("Name", scriptPath)
                            .InvokeAndClearCommands<ExternalScriptInfo>()[0].Parameters;
                        _pwsh.AddCommand(scriptPath, s_UseLocalScope);
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
                        _logger.LogInformation(psobject.ToString());
                    }
                    
                    returnObject = pipelineItems[pipelineItems.Count - 1];
                }
                
                var result = _pwsh.AddCommand("Get-OutputBinding", s_UseLocalScope).InvokeAndClearCommands<Hashtable>()[0];

                if(returnObject != null)
                {
                    result.Add("$return", returnObject);
                }
                ResetRunspace();
                return result;
            }
            catch(Exception e)
            {
                ResetRunspace();
                throw;
            }
        }

        void ResetRunspace()
        {
            // Reset the runspace to the Initial Session State
            _pwsh.Runspace.ResetRunspaceState();

            // TODO: Change this to clearing the variable by running in the module
            string modulePath = System.IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Azure.Functions.PowerShell.Worker.Module", "Azure.Functions.PowerShell.Worker.Module.psd1");
            _pwsh.AddCommand("Import-Module")
                .AddParameter("Name", modulePath)
                .AddParameter("Scope", "Global")
                .AddParameter("Force")
                .InvokeAndClearCommands();
        }
    }
}
