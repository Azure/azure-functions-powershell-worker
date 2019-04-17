// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RpcChannelEvent : ScriptEvent
    {
        internal RpcChannelEvent(string workerId)
            : base(nameof(RpcChannelEvent), EventSources.Worker)
        {
            WorkerId = workerId ?? throw new ArgumentNullException(nameof(workerId));
        }

        internal string WorkerId { get; private set; }
    }
}