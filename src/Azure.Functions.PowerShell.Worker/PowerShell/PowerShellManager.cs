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

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    public class PowerShellManager
    {
        // This script handles when the user adds something to the pipeline.
        // It logs the item that comes and stores it as the $return out binding.
        // The last item stored as $return will be returned to the function host.

        readonly static string s_LogAndSetReturnValueScript = @"
param([Parameter(ValueFromPipeline=$true)]$return)

$return | Out-Default

Set-Variable -Name '$return' -Value $return -Scope global
";

        readonly static string s_TriggerMetadataParameterName = "TriggerMetadata";

        RpcLogger _logger;
        PowerShell _pwsh;

        PowerShellManager(PowerShell pwsh, RpcLogger logger)
        {
            _pwsh = pwsh;
            _logger = logger;
        }

        public static PowerShellManager Create(PowerShell pwsh, RpcLogger logger)
        {
            var manager = new PowerShellManager(pwsh, logger);

            // Setup Stream event listeners
            var streamHandler = new StreamHandler(logger);
            pwsh.Streams.Debug.DataAdded += streamHandler.DebugDataAdded;
            pwsh.Streams.Error.DataAdded += streamHandler.ErrorDataAdded;
            pwsh.Streams.Information.DataAdded += streamHandler.InformationDataAdded;
            pwsh.Streams.Progress.DataAdded += streamHandler.ProgressDataAdded;
            pwsh.Streams.Verbose.DataAdded += streamHandler.VerboseDataAdded;
            pwsh.Streams.Warning.DataAdded += streamHandler.WarningDataAdded;

            manager.ResetRunspace();

            return manager;
        }

        static string BuildBindingHashtableScript(IDictionary<string, BindingInfo> outBindings)
        {
            // Since all of the out bindings are stored in variables at this point,
            // we must construct a script that will return those output bindings in a hashtable
            StringBuilder script = new StringBuilder();
            script.AppendLine("@{");
            foreach (KeyValuePair<string, BindingInfo> binding in outBindings)
            {
                script.Append("'");
                script.Append(binding.Key);

                // since $return has a dollar sign, we have to treat it differently
                if (binding.Key == "$return")
                {
                    script.Append("' = ");
                }
                else
                {
                    script.Append("' = $");
                }
                script.AppendLine(binding.Key);
            }
            script.AppendLine("}");

            return script.ToString();
        }

        void ResetRunspace()
        {
            // Reset the runspace to the Initial Session State
            _pwsh.Runspace.ResetRunspaceState();

            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            ExecuteScriptAndClearCommands($"using namespace {typeof(HttpResponseContext).Namespace}");
        }

        void ExecuteScriptAndClearCommands(string script)
        {
            _pwsh.AddScript(script).Invoke();
            _pwsh.Commands.Clear();
        }

        public Collection<T> ExecuteScriptAndClearCommands<T>(string script)
        {
            var result = _pwsh.AddScript(script).Invoke<T>();
            _pwsh.Commands.Clear();
            return result;
        }

        public PowerShellManager InvokeFunctionAndSetGlobalReturn(
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
                        ExecuteScriptAndClearCommands($@". {scriptPath}");
                        parameterMetadata = ExecuteScriptAndClearCommands<FunctionInfo>($@"Get-Command {entryPoint}")[0].Parameters;
                        _pwsh.AddScript($@". {entryPoint} @args");

                    }
                    else
                    {
                        parameterMetadata = ExecuteScriptAndClearCommands<ExternalScriptInfo>($@"Get-Command {scriptPath}")[0].Parameters;
                        _pwsh.AddScript($@". {scriptPath} @args");
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

                // This script handles when the user adds something to the pipeline.
                using (ExecutionTimer.Start(_logger, "Execution of the user's function completed."))
                {
                    ExecuteScriptAndClearCommands(s_LogAndSetReturnValueScript);
                }
                return this;
            }
            catch(Exception e)
            {
                ResetRunspace();
                throw e;
            }
        }

        public Hashtable ReturnBindingHashtable(IDictionary<string, BindingInfo> outBindings)
        {
            try
            {
                // This script returns a hashtable that contains the
                // output bindings that we will return to the function host.
                var result = ExecuteScriptAndClearCommands<Hashtable>(BuildBindingHashtableScript(outBindings))[0];
                ResetRunspace();
                return result;
            }
            catch(Exception e)
            {
                ResetRunspace();
                throw e;
            }
        }
    }
}