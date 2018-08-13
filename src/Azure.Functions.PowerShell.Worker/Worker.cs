using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Azure.Functions.PowerShell.Worker.Messaging;
using Microsoft.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class Worker
    {
        private static FunctionMessagingClient s_client;
        private static System.Management.Automation.PowerShell s_ps;
        private static FunctionLoader s_FunctionLoader = new FunctionLoader();
        public async static Task Main(string[] args)
        {
            if (args.Length != 10)
            {
                Console.WriteLine("usage --host <host> --port <port> --workerId <workerId> --requestId <requestId> --grpcMaxMessageLength <length>");
                return;
            }
            StartupArguments startupArguments = StartupArguments.Parse(args);

            s_client = new FunctionMessagingClient(startupArguments.Host, startupArguments.Port);
            InitPowerShell();

            var streamingMessage = new StreamingMessage() {
                RequestId = startupArguments.RequestId,
                StartStream = new StartStream() { WorkerId = startupArguments.WorkerId }
            };
            await s_client.WriteAsync(streamingMessage);

            await ProcessEvent();
        }

        private static void InitPowerShell()
        {
            s_ps = System.Management.Automation.PowerShell.Create(InitialSessionState.CreateDefault2());
            s_ps.AddScript("$PSHOME");
            //s_ps.AddCommand("Set-ExecutionPolicy").AddParameter("ExecutionPolicy", ExecutionPolicy.Unrestricted).AddParameter("Scope", ExecutionPolicyScope.Process);
            var result = s_ps.Invoke<string>();
            s_ps.Commands.Clear();

            Console.WriteLine(result[0]);
        }

        private static async Task ProcessEvent()
        {
            using (s_client)
            {
                while (await s_client.MoveNext())
                {
                    var message = s_client.GetCurrentMessage();
                    switch (message.ContentCase)
                    {
                        case StreamingMessage.ContentOneofCase.WorkerInitRequest:
                            await HandleWorkerInitRequest(message.RequestId, message.WorkerInitRequest);
                            break;

                        case StreamingMessage.ContentOneofCase.FunctionLoadRequest:
                            await HandleFunctionLoadRequest(message.RequestId, message.FunctionLoadRequest);
                            break;

                        case StreamingMessage.ContentOneofCase.InvocationRequest:
                            await HandleInvocationRequest(message.RequestId, message.InvocationRequest);
                            break;

                        default:
                            throw new InvalidOperationException($"Not supportted message type: {message.ContentCase}");
                    }
                }
            }
        }

        private static async Task HandleWorkerInitRequest(string requestId, WorkerInitRequest initRequest)
        {
            var response = new StreamingMessage()
            {
                RequestId = requestId,
                WorkerInitResponse = new WorkerInitResponse()
                {
                    Result = new StatusResult()
                    {
                        Status = StatusResult.Types.Status.Success
                    }
                }
            };
            await s_client.WriteAsync(response);
        }

        private static async Task HandleFunctionLoadRequest(string requestId, FunctionLoadRequest loadRequest)
        {
            s_FunctionLoader.Load(loadRequest.FunctionId, loadRequest.Metadata);
            var response = new StreamingMessage()
            {
                RequestId = requestId,
                FunctionLoadResponse = new FunctionLoadResponse()
                {
                    FunctionId = loadRequest.FunctionId,
                    Result = new StatusResult()
                    {
                        Status = StatusResult.Types.Status.Success
                    }
                }
            };
            await s_client.WriteAsync(response);
        }

        private static async Task HandleInvocationRequest(string requestId, InvocationRequest request)
        {
            var status = new StatusResult() { Status = StatusResult.Types.Status.Success };
            var response = new StreamingMessage()
            {
                RequestId = requestId,
                InvocationResponse = new InvocationResponse()
                {
                    InvocationId = request.InvocationId,
                    Result = status
                }
            };

            var info = s_FunctionLoader.GetInfo(request.FunctionId);
            // (Context context, List<TypedData> inputs) = Context.CreateContextAndInputs(info, request);
            (string scriptPath, string entryPoint) = s_FunctionLoader.GetFunc(request.FunctionId);
            
            if(entryPoint != "")
            {
                s_ps.AddCommand(entryPoint);
            }
            else
            {
                s_ps.AddCommand(scriptPath);
            }

            foreach (ParameterBinding binding in request.InputData)
            {
                s_ps.AddParameter(binding.Name, TypeConverter.FromTypedData(binding.Data));
            }

            // s_ps.AddParameter("context", context);
            // foreach (TypedData input in inputs)
            // {
            //     s_ps.AddArgument(input);
            // }
            PSObject result = null;
            try
            {
                result = s_ps.Invoke()[0];
            }
            finally
            {
                s_ps.Commands.Clear();
            }

            foreach (var binding in info.OutputBindings)
            {
                ParameterBinding paramBinding = new ParameterBinding()
                {
                    Name = binding.Key,
                    Data = TypeConverter.ToTypedData(
                        binding.Key,
                        binding.Value,
                        result)
                };

                // Not exactly sure which one to use for what scenario, so just set both.
                response.InvocationResponse.OutputData.Add(paramBinding);

                if(binding.Key == "$return")
                {
                    response.InvocationResponse.ReturnValue = paramBinding.Data;
                }
            }

            await s_client.WriteAsync(response);
        }
    }
}
