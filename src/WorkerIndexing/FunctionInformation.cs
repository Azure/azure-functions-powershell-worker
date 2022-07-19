using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing
{
    internal class FunctionInformation
    {
        public string Directory { get; set; } = "";
        public string ScriptFile { get; set; } = "";
        public string Name { get; set; } = "";
        public string EntryPoint { get; set; } = "";
        public string FunctionId { get; set; } = "";
        public List<BindingInformation> Bindings { get; set; } = new List<BindingInformation>();

        internal RpcFunctionMetadata ConvertToRpc()
        {
            RpcFunctionMetadata returnMetadata = new RpcFunctionMetadata();
            returnMetadata.FunctionId = FunctionId;
            returnMetadata.Directory = Directory;
            returnMetadata.EntryPoint = EntryPoint;
            returnMetadata.Name = Name;
            returnMetadata.ScriptFile = ScriptFile;
            returnMetadata.Language = "powershell";
            foreach(BindingInformation binding in Bindings)
            {
                string rawBinding = binding.ConvertToRpcRawBinding(out BindingInfo bindingInfo);
                returnMetadata.Bindings.Add(binding.Name, bindingInfo);
                returnMetadata.RawBindings.Add(rawBinding);
            }
            return returnMetadata;
        }
    }
}
