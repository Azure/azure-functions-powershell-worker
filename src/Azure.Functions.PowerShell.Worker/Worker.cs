//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using System.Management.Automation.Runspaces;

using Azure.Functions.PowerShell.Worker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
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

        static void InitPowerShell()
        {
            s_ps = System.Management.Automation.PowerShell.Create(InitialSessionState.CreateDefault());
            
            // Setup Stream event listeners
            var streamHandler = new StreamHandler(s_logger);
            s_ps.Streams.Debug.DataAdded += streamHandler.DebugDataAdded;
            s_ps.Streams.Error.DataAdded += streamHandler.ErrorDataAdded;
            s_ps.Streams.Information.DataAdded += streamHandler.InformationDataAdded;
            s_ps.Streams.Progress.DataAdded += streamHandler.ProgressDataAdded;
            s_ps.Streams.Verbose.DataAdded += streamHandler.VerboseDataAdded;
            s_ps.Streams.Warning.DataAdded += streamHandler.WarningDataAdded;

            // Add HttpResponseContext namespace so users can reference
            // HttpResponseContext without needing to specify the full namespace
            s_ps.AddScript($"using namespace {typeof(HttpResponseContext).Namespace}").Invoke();
            s_ps.Commands.Clear();
            s_ps.Runspace.ResetRunspaceState();
        }

        public async static Task Main(string[] args)
        {
            StartupArguments startupArguments = StartupArguments.Parse(args);

            // Initialize Rpc client, logger, and PowerShell
            s_client = new FunctionMessagingClient(startupArguments.Host, startupArguments.Port);
            s_logger = new RpcLogger(s_client);
            InitPowerShell();

            // Send StartStream message
            var streamingMessage = new StreamingMessage() {
                RequestId = startupArguments.RequestId,
                StartStream = new StartStream() { WorkerId = startupArguments.WorkerId }
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
}