using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal static class WorkerIndexingHelper
    {
        private static Dictionary<string, List<string>> functions = new Dictionary<string, List<string>>();
        private static Dictionary<string, BindingInfo> bindings = new Dictionary<string, BindingInfo>();

        internal static void RegisterFunction(string functionName, List<String> bindingNames)
        {
            functions[functionName] = bindingNames;
        }

        internal static void RegisterBinding(string bindingName, BindingInfo binding)
        {
            bindings[bindingName] = binding;
        }

        internal static FunctionMetadataResponses FormatMetadata()
        {

        }
    }
}
