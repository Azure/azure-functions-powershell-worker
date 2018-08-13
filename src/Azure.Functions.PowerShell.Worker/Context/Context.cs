using System.Collections.Generic;
using Google.Protobuf.Collections;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class Context
    {
        public MapField<string, BindingInfo> Bindings {get; set;}
        public MapField<string, TypedData> BindingData {get; set;}
        public ExecutionContext ExecutionContext {get; set;}
        public string InvocationId {get; set;}
        public ContextHttpRequest Request {get; set;}
        public ContextHttpResponse Response {get; set;}

        public Context (FunctionInfo info, InvocationRequest request)
        {
            InvocationId = request.InvocationId;
            Bindings = new MapField<string, BindingInfo>();
            ExecutionContext = new ExecutionContext
            {
                InvocationId = request.InvocationId,
                FunctionName = info.Name,
                FunctionDirectory = info.Directory
            };
            

            BindingData = request.TriggerMetadata;
            BindingData.Add("InvocationId", new TypedData {
                String = InvocationId
            });
        }

        public static (Context context, List<TypedData> inputs) CreateContextAndInputs(FunctionInfo info, InvocationRequest request)
        {
            var context = new Context(info, request);
            List<TypedData> inputs = new List<TypedData>(); 
            ContextHttpRequest httpInput = null;
            foreach (var binding in request.InputData)
            {
                if (binding.Name != null && binding.Data != null )
                {
                    if (binding.Data.Http != null)
                    {
                        httpInput = TypeConverter.ToContextHttp(binding.Data.Http);
                    }
                    inputs.Add(binding.Data);
                }
            }

            if (httpInput != null)
            {
                context.Request = httpInput;
                context.Response = new ContextHttpResponse();
            }

            return (context, inputs);
        }
    }

    public class ExecutionContext
    {
        public string InvocationId {get; internal set;}
        public string FunctionName {get; internal set;}
        public string FunctionDirectory {get; internal set;}
    }
}