using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using System.Management.Automation;

    public static class PowerShellWorkerExtensions
    {
        // This script handles when the user adds something to the pipeline.
        // It logs the item that comes and stores it as the $return out binding.
        // The last item stored as $return will be returned to the function host.

        private static string s_LogAndSetReturnValueScript = @"
param([Parameter(ValueFromPipeline=$true)]$return)

$return | Out-Default

Set-Variable -Name '$return' -Value $return -Scope global
";

        public static PowerShell SetGlobalVariables(this PowerShell ps, Hashtable triggerMetadata, IList<ParameterBinding> inputData)
        {
            try {
                // Set the global $Context variable which contains trigger metadata
                ps.AddCommand("Set-Variable").AddParameters( new Hashtable {
                    { "Name", "Context"},
                    { "Scope", "Global"},
                    { "Value", triggerMetadata}
                }).Invoke();

                // Sets a global variable for each input binding
                foreach (ParameterBinding binding in inputData)
                {
                    ps.AddCommand("Set-Variable").AddParameters( new Hashtable {
                        { "Name", binding.Name},
                        { "Scope", "Global"},
                        { "Value",  binding.Data.ToObject()}
                    }).Invoke();
                }
                return ps;
            }
            catch(Exception e)
            {
                ps.CleanupRunspace();
                throw e;
            }
        }

        public static PowerShell InvokeFunctionAndSetGlobalReturn(this PowerShell ps, string scriptPath, string entryPoint)
        {
            try
            {
                // We need to take into account if the user has an entry point.
                // If it does, we invoke the command of that name
                if(entryPoint != "")
                {
                    ps.AddScript($@". {scriptPath}").Invoke();
                    ps.AddScript($@". {entryPoint}");
                }
                else
                {
                    ps.AddScript($@". {scriptPath}");
                }

                // This script handles when the user adds something to the pipeline.
                ps.AddScript(s_LogAndSetReturnValueScript).Invoke();
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
                var result = ps.AddScript(BuildBindingHashtableScript(outBindings)).Invoke<Hashtable>()[0];
                ps.Commands.Clear();
                return result;
            }
            catch(Exception e)
            {
                ps.CleanupRunspace();
                throw e;
            }
        }

        private static string BuildBindingHashtableScript(IDictionary<string, BindingInfo> outBindings)
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
        private static void CleanupRunspace(this PowerShell ps)
        {
            ps.Commands.Clear();
        }
    }
}