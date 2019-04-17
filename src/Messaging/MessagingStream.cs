//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.Messaging
{
    internal class MessagingStream
    {
        private readonly AsyncDuplexStreamingCall<StreamingMessage, StreamingMessage> _call;
        private BlockingCollection<StreamingMessage> _blockingCollectionQueue = new BlockingCollection<StreamingMessage>();

        internal MessagingStream(string host, int port)
        {
            Channel channel = new Channel(host, port, ChannelCredentials.Insecure);
            _call = new FunctionRpc.FunctionRpcClient(channel).EventStream();
        }

        /// <summary>
        /// Get the current message.
        /// </summary>
        internal StreamingMessage GetCurrentMessage() => _call.ResponseStream.Current;

        internal void AddToBlockingQueue(StreamingMessage streamingMessage)
        {
            _blockingCollectionQueue.Add(streamingMessage);
        }

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
            var consumer = Task.Run(async () =>
            {
                foreach (var rpcWriteMsg in _blockingCollectionQueue.GetConsumingEnumerable())
                {
                    await _call.RequestStream.WriteAsync(rpcWriteMsg);
                }
            });
            await consumer;
        }
    }
}
