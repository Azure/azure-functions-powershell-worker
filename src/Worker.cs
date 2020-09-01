//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

using CommandLine;
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    /// <summary>
    /// The PowerShell language worker for Azure Function
    /// </summary>
    public static class Worker
    {
        /// <summary>
        /// Entry point of the language worker.
        /// </summary>
        public async static Task Main(string[] args)
        {
            RpcLogger.WriteSystemLog(
                LogLevel.Information,
                string.Format(PowerShellWorkerStrings.PowerShellWorkerVersion, typeof(Worker).Assembly.GetName().Version));

            WorkerArguments arguments = null;
            Parser.Default.ParseArguments<WorkerArguments>(args)
                .WithParsed(ops => arguments = ops)
                .WithNotParsed(err => Environment.Exit(1));

            InitialSessionStateProvider.Initialize();

            var msgStream = new MessagingStream(arguments.Host, arguments.Port);
            var requestProcessor = new RequestProcessor(msgStream);

            // Send StartStream message
            var startedMessage = new StreamingMessage() {
                RequestId = arguments.RequestId,
                StartStream = new StartStream() { WorkerId = arguments.WorkerId }
            };

            msgStream.Write(startedMessage);
            await requestProcessor.ProcessRequestLoop();
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

        [Option("grpcMaxMessageLength", Required = false, HelpText = "[Deprecated and ignored] gRPC Maximum message size.")]
        public int MaxMessageLength { get; set; }
    }
}
