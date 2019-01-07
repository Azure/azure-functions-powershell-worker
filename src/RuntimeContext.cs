//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RuntimeContext
    {
        public RuntimeContext(MessagingStream msgStream, string managedModulePath)
        {
            this.MsgStream = msgStream;
            this.ManagedModulePath = managedModulePath;
        }

        internal MessagingStream MsgStream { get; private set; }

        internal string ManagedModulePath {get; private set;}
    }
}
