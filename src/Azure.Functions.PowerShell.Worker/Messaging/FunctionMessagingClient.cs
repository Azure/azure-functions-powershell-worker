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
    internal class FunctionMessagingClient : IDisposable
    {
        SemaphoreSlim _writeStreamHandle = new SemaphoreSlim(1, 1);
        AsyncDuplexStreamingCall<StreamingMessage, StreamingMessage> _call;
        public bool isDisposed;

        public FunctionMessagingClient(string host, int port)
        {
            Channel channel = new Channel(host, port, ChannelCredentials.Insecure);
            _call = new FunctionRpc.FunctionRpcClient(channel).EventStream();
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                _call.Dispose();
            }
        }

        public StreamingMessage GetCurrentMessage() =>
            isDisposed ? null : _call.ResponseStream.Current;

        public async Task<bool> MoveNext() =>
            !isDisposed && await _call.ResponseStream.MoveNext(CancellationToken.None);

        public async Task WriteAsync(StreamingMessage message)
        {
            if(isDisposed) return;

            // Wait for the handle to be released because we can't have
            // more than one message being sent at the same time
            await _writeStreamHandle.WaitAsync();
            try
            {
                await _call.RequestStream.WriteAsync(message);
            }
            finally
            {
                _writeStreamHandle.Release();
            }
        }
    }
}