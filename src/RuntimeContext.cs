//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using System;
using System.IO;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RuntimeContext
    {
        public RuntimeContext(MessagingStream msgStream, string managedModulePath)
        {
            if (!IsModulePathValid(managedModulePath))
            {
                throw new ArgumentException("Invalid managed module path: '{0}'", managedModulePath);
            }
            this.MsgStream = msgStream;
            this.ManagedModulePath = managedModulePath;
        }

        internal MessagingStream MsgStream { get; private set; }

        internal string ManagedModulePath { get; private set; }

        /// <summary>
        /// Checkes if the managed module path is valid
        /// </summary>
        /// <param name="managedModulePath">Path</param>
        /// <returns>True if (i) path is not specified or (ii) specified path exists. False otherwise.</returns>
        private bool IsModulePathValid(string managedModulePath)
        {   // Empty/null path is okay.  If a path is supplied, it should be a valid path
            return string.IsNullOrEmpty(managedModulePath) || Directory.Exists(managedModulePath);
        }
    }
}
