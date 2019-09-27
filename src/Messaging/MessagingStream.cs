//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.Messaging
{
    internal class MessagingStream
    {
        private readonly AsyncDuplexStreamingCall<StreamingMessage, StreamingMessage> _call;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        internal MessagingStream(string host, int port, int maxReceiveMessageLength)
        {
            var channelOptions = new []
            {
                new ChannelOption(ChannelOptions.MaxReceiveMessageLength, maxReceiveMessageLength)
            };

            Channel channel = new Channel(host, port, ChannelCredentials.Insecure, channelOptions);
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
