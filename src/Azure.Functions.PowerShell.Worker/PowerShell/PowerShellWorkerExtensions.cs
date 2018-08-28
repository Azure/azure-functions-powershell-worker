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

    public static class PowerShellWorkerExtensions
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

        // TODO: make sure this completely cleans up the runspace
        static void CleanupRunspace(this PowerShell ps)
        {
            // Reset the runspace to the Initial Session State
            ps.Runspace.ResetRunspaceState();

            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            ps.ExecuteScriptAndClearCommands($"using namespace {typeof(HttpResponseContext).Namespace}");
        }

        static void ExecuteScriptAndClearCommands(this PowerShell ps, string script)
        {
            ps.AddScript(script).Invoke();
            ps.Commands.Clear();
        }

        public static Collection<T> ExecuteScriptAndClearCommands<T>(this PowerShell ps, string script)
        {
            var result = ps.AddScript(script).Invoke<T>();
            ps.Commands.Clear();
            return result;
        }

        public static PowerShell InvokeFunctionAndSetGlobalReturn(
            this PowerShell ps,
            string scriptPath,
            string entryPoint,
            Hashtable triggerMetadata,
            IList<ParameterBinding> inputData,
            RpcLogger logger)
        {
            try
            {
                Dictionary<string, ParameterMetadata> parameterMetadata;

                // We need to take into account if the user has an entry point.
                // If it does, we invoke the command of that name. We also need to fetch
                // the ParameterMetadata so that we can tell whether or not the user is asking
                // for the $TriggerMetadata

                using (ExecutionTimer.Start(logger, "Parameter metadata retrieved."))
                {
                    if (entryPoint != "")
                    {
                        ps.ExecuteScriptAndClearCommands($@". {scriptPath}");
                        parameterMetadata = ps.ExecuteScriptAndClearCommands<FunctionInfo>($@"Get-Command {entryPoint}")[0].Parameters;
                        ps.AddScript($@". {entryPoint} @args");

                    }
                    else
                    {
                        parameterMetadata = ps.ExecuteScriptAndClearCommands<ExternalScriptInfo>($@"Get-Command {scriptPath}")[0].Parameters;
                        ps.AddScript($@". {scriptPath} @args");
                    }
                }

                // Sets the variables for each input binding
                foreach (ParameterBinding binding in inputData)
                {
                    ps.AddParameter(binding.Name, binding.Data.ToObject());
                }

                // Gives access to additional Trigger Metadata if the user specifies TriggerMetadata
                if(parameterMetadata.ContainsKey(s_TriggerMetadataParameterName))
                {
                    ps.AddParameter(s_TriggerMetadataParameterName, triggerMetadata);
                    logger.LogDebug($"TriggerMetadata found. Value:{Environment.NewLine}{triggerMetadata.ToString()}");
                }

                // This script handles when the user adds something to the pipeline.
                using (ExecutionTimer.Start(logger, "Execution of the user's function completed."))
                {
                    ps.ExecuteScriptAndClearCommands(s_LogAndSetReturnValueScript);
                }
                return ps;
            }
            catch(Exception e)
            {
                ps.CleanupRunspace();
                throw e;
            }
        }

        public static Hashtable ReturnBindingHashtable(this PowerShell ps, IDictionary<string, BindingInfo> outBindings)
        {
            try
            {
                // This script returns a hashtable that contains the
                // output bindings that we will return to the function host.
                var result = ps.ExecuteScriptAndClearCommands<Hashtable>(BuildBindingHashtableScript(outBindings))[0];
                ps.CleanupRunspace();
                return result;
            }
            catch(Exception e)
            {
                ps.CleanupRunspace();
                throw e;
            }
        }
    }
}