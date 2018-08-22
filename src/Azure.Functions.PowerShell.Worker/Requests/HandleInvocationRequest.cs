using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Extensions.Logging;

namespace  Microsoft.Azure.Functions.PowerShellWorker.Requests
{
    using System.Management.Automation;
    using System.Text;

    public class HandleInvocationRequest
    {
        public static StreamingMessage Invoke(
            PowerShell powershell,
            FunctionLoader functionLoader,
            StreamingMessage request,
            RpcLogger logger)
        {
            InvocationRequest invocationRequest = request.InvocationRequest;
            logger.SetContext(request.RequestId, invocationRequest.InvocationId);

            var status = new StatusResult() { Status = StatusResult.Types.Status.Success };
            var response = new StreamingMessage()
            {
                RequestId = request.RequestId,
                InvocationResponse = new InvocationResponse()
                {
                    InvocationId = invocationRequest.InvocationId,
                    Result = status
                }
            };

            var info = functionLoader.GetInfo(invocationRequest.FunctionId);

            // Add $Context variable, which contains trigger metadata, to the Global scope
            Hashtable triggerMetadata = new Hashtable();
            foreach (var dataItem in invocationRequest.TriggerMetadata)
            {
                triggerMetadata.Add(dataItem.Key, TypeConverter.FromTypedData(dataItem.Value));
            }

            if (triggerMetadata.Count > 0)
            {
                powershell.AddCommand("Set-Variable").AddParameters( new Hashtable {
                    { "Name", "Context"},
                    { "Scope", "Global"},
                    { "Value", triggerMetadata}
                });
                powershell.Invoke();
            }

            foreach (ParameterBinding binding in invocationRequest.InputData)
            {
                powershell.AddCommand("Set-Variable").AddParameters( new Hashtable {
                    { "Name", binding.Name},
                    { "Scope", "Global"},
                    { "Value",  TypeConverter.FromTypedData(binding.Data)}
                });
                powershell.Invoke();
            }

            // foreach (KeyValuePair<string, BindingInfo> binding in info.OutputBindings)
            // {
            //     powershell.AddCommand("Set-Variable").AddParameters( new Hashtable {
            //         { "Name", binding.Key},
            //         { "Scope", "Global"},
            //         { "Value", null}
            //     });
            //     powershell.Invoke();
            // }

            (string scriptPath, string entryPoint) = functionLoader.GetFunc(invocationRequest.FunctionId);
            
            if(entryPoint != "")
            {
                powershell.AddScript($@". {scriptPath}");
                powershell.Invoke();
                powershell.AddCommand(entryPoint);
            }
            else
            {
                powershell.AddCommand(scriptPath);
            }

            powershell.AddScript(@"
param([Parameter(ValueFromPipeline=$true)]$return)

$return | Out-Default

Set-Variable -Name '$return' -Value $return -Scope global
");

            StringBuilder script = new StringBuilder();
            script.AppendLine("@{");
            foreach (KeyValuePair<string, BindingInfo> binding in info.OutputBindings)
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

            Hashtable result = null;
            try
            {
                powershell.Invoke();
                powershell.AddScript(script.ToString());
                result = powershell.Invoke<Hashtable>()[0];
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = TypeConverter.ToRpcException(e);
                powershell.Commands.Clear();
                return response;
            }
            powershell.Commands.Clear();

            foreach (KeyValuePair<string, BindingInfo> binding in info.OutputBindings)
            {
                ParameterBinding paramBinding = new ParameterBinding()
                {
                    Name = binding.Key,
                    Data = TypeConverter.ToTypedData(
                        result[binding.Key])
                };

                response.InvocationResponse.OutputData.Add(paramBinding);

                if(binding.Key == "$return")
                {
                    response.InvocationResponse.ReturnValue = paramBinding.Data;
                }
            }

            return response;
        }
    }
}