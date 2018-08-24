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
using Microsoft.Azure.Functions.PowerShellWorker.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell.Host;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class Worker
    {
        private static FunctionMessagingClient s_client;
        private static System.Management.Automation.PowerShell s_ps;
        private static Runspace s_runspace;
        private static FunctionLoader s_FunctionLoader = new FunctionLoader();
        private static RpcLogger s_Logger;
        public async static Task Main(string[] args)
        {
            if (args.Length != 10)
            {
                Console.WriteLine("usage --host <host> --port <port> --workerId <workerId> --requestId <requestId> --grpcMaxMessageLength <length>");
                return;
            }
            StartupArguments startupArguments = StartupArguments.Parse(args);

            // Initialize Rpc client, logger, and PowerShell
            s_client = new FunctionMessagingClient(startupArguments.Host, startupArguments.Port);
            s_Logger = new RpcLogger(s_client);
            InitPowerShell();

            // Send StartStream message
            var streamingMessage = new StreamingMessage() {
                RequestId = startupArguments.RequestId,
                StartStream = new StartStream() { WorkerId = startupArguments.WorkerId }
            };

            await s_client.WriteAsync(streamingMessage);

            await ProcessEvent();
        }

        private static void InitPowerShell()
        {
            var host = new AzureFunctionsHost(s_Logger);

            s_runspace = RunspaceFactory.CreateRunspace(host);
            s_runspace.Open();
            s_ps = System.Management.Automation.PowerShell.Create(InitialSessionState.CreateDefault());
            s_ps.Runspace = s_runspace;

            s_ps.AddScript("$PSHOME");
            //s_ps.AddCommand("Set-ExecutionPolicy").AddParameter("ExecutionPolicy", ExecutionPolicy.Unrestricted).AddParameter("Scope", ExecutionPolicyScope.Process);
            s_ps.Invoke<string>();

            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            s_ps.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}").Invoke();
            s_ps.Commands.Clear();
        }

        private static async Task ProcessEvent()
        {
            using (s_client)
            {
                while (await s_client.MoveNext())
                {
                    var message = s_client.GetCurrentMessage();
                    StreamingMessage response;
                    switch (message.ContentCase)
                    {
                        case StreamingMessage.ContentOneofCase.WorkerInitRequest:
                            response = HandleWorkerInitRequest.Invoke(
                                s_ps,
                                s_FunctionLoader,
                                message,
                                s_Logger);
                            break;

                        case StreamingMessage.ContentOneofCase.FunctionLoadRequest:
                            response = HandleFunctionLoadRequest.Invoke(
                                s_ps,
                                s_FunctionLoader,
                                message,
                                s_Logger);
                            break;

                        case StreamingMessage.ContentOneofCase.InvocationRequest:
                            response = HandleInvocationRequest.Invoke(
                                s_ps,
                                s_FunctionLoader,
                                message,
                                s_Logger);
                            break;

                        default:
                            throw new InvalidOperationException($"Not supportted message type: {message.ContentCase}");
                    }

                    await s_client.WriteAsync(response);
                }
            }
        }
    }
}