//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
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

            var workerOptions = new WorkerOptions();

            var parser = new Parser(settings =>
            {
                settings.EnableDashDash = true;
                settings.IgnoreUnknownArguments = true;
            });
            parser.ParseArguments<WorkerArguments>(args)
                .WithParsed(workerArgs =>
                {
                    // TODO: Remove parsing old command-line arguments that are not prefixed with functions-<argumentname>
                    // for more information, see https://github.com/Azure/azure-functions-powershell-worker/issues/995
                    workerOptions.WorkerId = workerArgs.FunctionsWorkerId ?? workerArgs.WorkerId;
                    workerOptions.RequestId = workerArgs.FunctionsRequestId ?? workerArgs.RequestId;

                    if (!string.IsNullOrWhiteSpace(workerArgs.FunctionsUri))
                    {
                        try
                        {
                            // TODO: Update WorkerOptions to have a URI property instead of host name and port number
                            // for more information, see https://github.com/Azure/azure-functions-powershell-worker/issues/994
                            var uri = new Uri(workerArgs.FunctionsUri);
                            workerOptions.Host = uri.Host;
                            workerOptions.Port = uri.Port;
                        }
                        catch (UriFormatException formatEx)
                        {
                            var message = $"Invalid URI format: {workerArgs.FunctionsUri}. Error message: {formatEx.Message}";
                            throw new ArgumentException(message, nameof(workerArgs.FunctionsUri));
                        }
                    }
                    else
                    {
                        workerOptions.Host = workerArgs.Host;
                        workerOptions.Port = workerArgs.Port;
                    }

                    // Validate workerOptions
                    ValidateProperty("WorkerId", workerOptions.WorkerId);
                    ValidateProperty("RequestId", workerOptions.RequestId);
                    ValidateProperty("Host", workerOptions.Host);

                    if (workerOptions.Port <= 0)
                    {
                        throw new ArgumentException("Port number has not been initialized", nameof(workerOptions.Port));
                    }
                });

            // Create the very first Runspace so the debugger has the target to attach to.
            // This PowerShell instance is shared by the first PowerShellManager instance created in the pool,
            // and the dependency manager (used to download dependent modules if needed).
            var firstPowerShellInstance = Utils.NewPwshInstance();
            var pwshVersion = Utils.GetPowerShellVersion(firstPowerShellInstance);
            LogPowerShellVersion(pwshVersion);
            WarmUpPowerShell(firstPowerShellInstance);

            var msgStream = new MessagingStream(workerOptions.Host, workerOptions.Port);
            var requestProcessor = new RequestProcessor(msgStream, firstPowerShellInstance, pwshVersion);

            // Send StartStream message
            var startedMessage = new StreamingMessage()
            {
                RequestId = workerOptions.RequestId,
                StartStream = new StartStream() { WorkerId = workerOptions.WorkerId }
            };

            msgStream.Write(startedMessage);
            await requestProcessor.ProcessRequestLoop();
        }

        // Warm up the PowerShell instance so that the subsequent function load and invocation requests are faster
        private static void WarmUpPowerShell(System.Management.Automation.PowerShell firstPowerShellInstance)
        {
            // It turns out that creating/removing a function warms up the runspace enough.
            // We just need this name to be unique, so that it does not coincide with an actual function.
            const string DummyFunctionName = "DummyFunction-71b09c92-6bce-42d0-aba1-7b985b8c3563";

            firstPowerShellInstance.AddCommand("Microsoft.PowerShell.Management\\New-Item")
                .AddParameter("Path", "Function:")
                .AddParameter("Name", DummyFunctionName)
                .AddParameter("Value", ScriptBlock.Create(string.Empty))
                .InvokeAndClearCommands();

            firstPowerShellInstance.AddCommand("Microsoft.PowerShell.Management\\Remove-Item")
                .AddParameter("Path", $"Function:{DummyFunctionName}")
                .InvokeAndClearCommands();
        }

        private static void LogPowerShellVersion(string pwshVersion)
        {
            var message = string.Format(PowerShellWorkerStrings.PowerShellVersion, pwshVersion);
            RpcLogger.WriteSystemLog(LogLevel.Information, message);
        }

        private static void ValidateProperty(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{name} is null or empty", name);
            }
        }
    }

    internal class WorkerArguments
    {
        [Option("host", Required = false, HelpText = "IP Address used to connect to the Host via gRPC.")]
        public string Host { get; set; }

        [Option("port", Required = false, HelpText = "Port used to connect to the Host via gRPC.")]
        public int Port { get; set; }

        [Option("workerId", Required = false, HelpText = "Worker ID assigned to this language worker.")]
        public string WorkerId { get; set; }

        [Option("requestId", Required = false, HelpText = "Request ID used for gRPC communication with the Host.")]
        public string RequestId { get; set; }

        [Option("functions-uri", Required = false, HelpText = "URI with IP Address and Port used to connect to the Host via gRPC.")]
        public string FunctionsUri { get; set; }

        [Option("functions-workerid", Required = false, HelpText = "Worker ID assigned to this language worker.")]
        public string FunctionsWorkerId { get; set; }

        [Option("functions-requestid", Required = false, HelpText = "Request ID used for gRPC communication with the Host.")]
        public string FunctionsRequestId { get; set; }
    }

    internal class WorkerOptions
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public string WorkerId { get; set; }

        public string RequestId { get; set; }
    }
}
