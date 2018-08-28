//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

using CommandLine;
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell.Host;
using Microsoft.Azure.Functions.PowerShellWorker.Requests;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public static class Worker
    {
        static readonly FunctionLoader s_functionLoader = new FunctionLoader();
        static FunctionMessagingClient s_client;
        static RpcLogger s_logger;
        static System.Management.Automation.PowerShell s_ps;
        static Runspace s_runspace;

        static void InitPowerShell()
        {
            var host = new AzureFunctionsPowerShellHost(s_logger);

            s_runspace = RunspaceFactory.CreateRunspace(host);
            s_runspace.Open();
            s_ps = System.Management.Automation.PowerShell.Create();
            s_ps.Runspace = s_runspace;

            if (Platform.IsWindows)
            {
                s_ps.AddCommand("Set-ExecutionPolicy")
                    .AddParameter("ExecutionPolicy", "Unrestricted")
                    .AddParameter("Scope", "Process")
                    .Invoke();
                s_ps.Commands.Clear();
            }

            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            s_ps.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}").Invoke();
            s_ps.Commands.Clear();
        }

        public async static Task Main(string[] args)
        {
            WorkerArguments arguments = null;
            Parser.Default.ParseArguments<WorkerArguments>(args)
                .WithParsed(ops => arguments = ops)
                .WithNotParsed(err => Environment.Exit(1));

            // Initialize Rpc client, logger, and PowerShell
            s_client = new FunctionMessagingClient(arguments.Host, arguments.Port);
            s_logger = new RpcLogger(s_client);
            InitPowerShell();

            // Send StartStream message
            var streamingMessage = new StreamingMessage() {
                RequestId = arguments.RequestId,
                StartStream = new StartStream() { WorkerId = arguments.WorkerId }
            };

            await s_client.WriteAsync(streamingMessage);
            await ProcessEvent();
        }

        static async Task ProcessEvent()
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
                                s_functionLoader,
                                message,
                                s_logger);
                            break;

                        case StreamingMessage.ContentOneofCase.FunctionLoadRequest:
                            response = HandleFunctionLoadRequest.Invoke(
                                s_ps,
                                s_functionLoader,
                                message,
                                s_logger);
                            break;

                        case StreamingMessage.ContentOneofCase.InvocationRequest:
                            response = HandleInvocationRequest.Invoke(
                                s_ps,
                                s_functionLoader,
                                message,
                                s_logger);
                            break;

                        default:
                            throw new InvalidOperationException($"Not supportted message type: {message.ContentCase}");
                    }

                    await s_client.WriteAsync(response);
                }
            }
        }
    }

    internal class WorkerArguments
    {
        [Option("host", Required = true, HelpText = "IP Address used to connect to the Host via gRPC.")]
        public string Host { get; set; }

        [Option("port", Required = true, HelpText = "Port used to connect to the Host via gRPC.")]
        public int Port { get; set; }

        [Option("workerId", Required = true, HelpText = "Worker ID assigned to this language worker.")]
        public string WorkerId { get; set; }

        [Option("requestId", Required = true, HelpText = "Request ID used for gRPC communication with the Host.")]
        public string RequestId { get; set; }

        [Option("grpcMaxMessageLength", Required = true, HelpText = "gRPC Maximum message size.")]
        public int MaxMessageLength { get; set; }
    }
}