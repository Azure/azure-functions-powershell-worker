// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

using CommandLine;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.PowerShell.Worker
{
    public class WorkerEntry
    {
        public static void Main(string[] args)
        {
            LanguageWorker worker;
            Parser.Default.ParseArguments<ArgumentOptions>(args)
                .WithParsed(ops => worker = new LanguageWorker(ops))
                .WithNotParsed(err => Environment.Exit(1));
        }
    }

    public class ArgumentOptions
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

    internal class LanguageWorker
    {
        private ArgumentOptions _options;
        private FunctionRpc.FunctionRpcClient _client;
        private AsyncDuplexStreamingCall<StreamingMessage, StreamingMessage> _streamingCall;

        internal LanguageWorker(ArgumentOptions options)
        {
            var channel = new Channel(options.Host, options.Port, ChannelCredentials.Insecure);
            _client = new FunctionRpc.FunctionRpcClient(channel);
            _streamingCall = _client.EventStream();
            _options = options;
        }
    }
}
