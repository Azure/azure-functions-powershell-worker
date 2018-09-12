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
        private const string _TriggerMetadataParameterName = "TriggerMetadata";

        private RpcLogger _logger;
        private PowerShell _pwsh;

        internal PowerShellManager(RpcLogger logger)
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
            // Try to authenticate to Azure
            // TODO: The Azure Functions Host might supply these differently. This might change but works for the demo
            string applicationId = Environment.GetEnvironmentVariable("ApplicationId");
            string applicationSecret = Environment.GetEnvironmentVariable("ApplicationSecret");
            string tenantId = Environment.GetEnvironmentVariable("TenantId");

            if (string.IsNullOrEmpty(applicationId) ||
                string.IsNullOrEmpty(applicationSecret) ||
                string.IsNullOrEmpty(tenantId))
            {
                _logger.Log(LogLevel.Warning, "Required environment variables to authenticate to Azure were not present");
                return;
            }

            // Build SecureString
            var secureString = new SecureString();
            foreach (char item in applicationSecret)
            {
                secureString.AppendChar(item);
            }
            
            using (ExecutionTimer.Start(_logger, "Authentication to Azure completed."))
            {
                _pwsh.AddCommand("Connect-AzureRmAccount")
                    .AddParameter("Credential", new PSCredential(applicationId, secureString))
                    .AddParameter("ServicePrincipal")
                    .AddParameter("TenantId", tenantId)
                    .InvokeAndClearCommands();
            }
        }

        internal void InitializeRunspace()
        {
            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            _pwsh.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}").InvokeAndClearCommands();
            
            // Set the PSModulePath
            Environment.SetEnvironmentVariable("PSModulePath", Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Modules"));
            
            // TODO: remove this when we figure out why it fixed #48
            _pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameter("Name", "AzureRm.Netcore").InvokeAndClearCommands();

            AuthenticateToAzure();
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
                            .AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameter("Name", scriptPath)
                            .AddStatement()
                            .AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddParameter("Name", entryPoint)
                            .InvokeAndClearCommands<FunctionInfo>()[0].Parameters;

                        _pwsh.AddCommand(entryPoint);

                    }
                    else
                    {
                        parameterMetadata = _pwsh.AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddParameter("Name", scriptPath)
                            .InvokeAndClearCommands<ExternalScriptInfo>()[0].Parameters;

                        _pwsh.AddCommand(scriptPath);
                    }
                }

                // Sets the variables for each input binding
                foreach (ParameterBinding binding in inputData)
                {
                    _pwsh.AddParameter(binding.Name, binding.Data.ToObject());
                }

                // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
                if(parameterMetadata.ContainsKey(_TriggerMetadataParameterName))
                {
                    _pwsh.AddParameter(_TriggerMetadataParameterName, triggerMetadata);
                    _logger.Log(LogLevel.Debug, $"TriggerMetadata found. Value:{Environment.NewLine}{triggerMetadata.ToString()}");
                }

                PSObject returnObject = null;
                using (ExecutionTimer.Start(_logger, "Execution of the user's function completed."))
                {
                    // Log everything we received from the pipeline and set the last one to be the ReturnObject
                    Collection<PSObject> pipelineItems = _pwsh.InvokeAndClearCommands<PSObject>();
                    if (pipelineItems.Count > 0)
                    {
                        foreach (var psobject in pipelineItems)
                        {
                            _logger.Log(LogLevel.Information, $"OUTPUT: {psobject.ToString()}");
                        }
                        returnObject =  pipelineItems[pipelineItems.Count - 1];
                    }
                }
                
                var result = _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Get-OutputBinding")
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
                ResetRunspace(scriptPath);
            }
        }

        private void ResetRunspace(string scriptPath)
        {
            // Reset the runspace to the Initial Session State
            _pwsh.Runspace.ResetRunspaceState();

            // If the function had an entry point, this will remove the module that was loaded
            var moduleName = Path.GetFileNameWithoutExtension(scriptPath);
            _pwsh.AddCommand("Microsoft.PowerShell.Core\\Remove-Module")
                .AddParameter("Name", moduleName)
                .AddParameter("ErrorAction", "SilentlyContinue")
                .InvokeAndClearCommands();
        }
    }
}
