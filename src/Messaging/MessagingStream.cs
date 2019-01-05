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
        private readonly BlockingCollection<StreamingMessage> _msgQueue;

        internal MessagingStream(string host, int port)
        {
            Channel channel = new Channel(host, port, ChannelCredentials.Insecure);
            _call = new FunctionRpc.FunctionRpcClient(channel).EventStream();

            _msgQueue = new BlockingCollection<StreamingMessage>();
            Task.Run(WriteImplAsync);
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
        /// Write the outgoing message to the buffer.
        /// </summary>
        internal void Write(StreamingMessage message) => _msgQueue.Add(message);

        /// <summary>
        /// Take a message from the buffer and write to the gRPC channel.
        /// </summary>
        private async Task WriteImplAsync()
        {
            while (true)
            {
                StreamingMessage msg = _msgQueue.Take();
                await _call.RequestStream.WriteAsync(msg);
            }
        }
    }
}
