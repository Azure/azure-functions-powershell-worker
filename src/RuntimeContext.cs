using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

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
