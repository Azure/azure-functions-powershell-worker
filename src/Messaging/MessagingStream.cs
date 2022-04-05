//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.Messaging
{
    internal class MessagingStream
    {
        private readonly AsyncDuplexStreamingCall<StreamingMessage, StreamingMessage> _call;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        internal MessagingStream(string host, int port)
        {
            // To call unsecured gRPC services, ensure the address starts with 'http' as opposed to 'https'.
            // For more detail, see https://docs.microsoft.com/en-us/aspnet/core/grpc/client?view=aspnetcore-6.0
            string uriString = $"http://{host}:{port}";
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out Uri grpcUri))
            {
                throw new InvalidOperationException($"The gRPC channel URI '{uriString}' could not be parsed.");
            }

            const int maxMessageLength = int.MaxValue;

            var channelOptions = new GrpcChannelOptions
            {
                MaxReceiveMessageSize = maxMessageLength,
                MaxSendMessageSize = maxMessageLength,
                Credentials = ChannelCredentials.Insecure
            };

            GrpcChannel channel = GrpcChannel.ForAddress(grpcUri, channelOptions);
            _call = new FunctionRpc.FunctionRpcClient(channel).EventStream();
        }

        /// <summary>
        /// Get the current message.
        /// </summary>
        internal StreamingMessage GetCurrentMessage() => _call.ResponseStream.Current;

        /// <summary>
        /// Move to the next message.
        /// </summary>
        internal async Task<bool> MoveNext() => await _call.ResponseStream.MoveNext(CancellationToken.None);

        /// <summary>
        /// Write the outgoing message.
        /// </summary>
        internal void Write(StreamingMessage message) => WriteImplAsync(message).ConfigureAwait(false);

        /// <summary>
        /// Take a message from the buffer and write to the gRPC channel.
        /// </summary>
        private async Task WriteImplAsync(StreamingMessage message)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                await _call.RequestStream.WriteAsync(message);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
    }
}
