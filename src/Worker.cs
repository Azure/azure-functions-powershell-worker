//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CommandLine;
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
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

            ValidateFunctionsWorkerRuntimeVersion();

            WorkerArguments arguments = null;
            Parser.Default.ParseArguments<WorkerArguments>(args)
                .WithParsed(ops => arguments = ops)
                .WithNotParsed(err => Environment.Exit(1));

            var msgStream = new MessagingStream(arguments.Host, arguments.Port, arguments.MaxMessageLength);
            var requestProcessor = new RequestProcessor(msgStream);

            // Send StartStream message
            var startedMessage = new StreamingMessage() {
                RequestId = arguments.RequestId,
                StartStream = new StartStream() { WorkerId = arguments.WorkerId }
            };

            msgStream.Write(startedMessage);
            await requestProcessor.ProcessRequestLoop();
        }

        private static void ValidateFunctionsWorkerRuntimeVersion()
        {
            var requestedVersion = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME_VERSION");
            if (requestedVersion != null
                // Assuming this code is running on Functions runtime v2, allow
                // PowerShell version 6 only (ignoring leading and trailing spaces, and the optional ~ in front of 6)
                && !Regex.IsMatch(requestedVersion, @"^\s*~?6\s*$"))
            {
                throw new InvalidOperationException($"Invalid FUNCTIONS_WORKER_RUNTIME_VERSION specified: {requestedVersion}");
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
