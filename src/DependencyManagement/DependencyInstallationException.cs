//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    internal class DependencyInstallationException : Exception
    {
        internal DependencyInstallationException()
        {
        }
        internal DependencyInstallationException(string message)
            :base(message)
        {
        }
        internal DependencyInstallationException(string message, Exception innException)
            : base(message, innException)
        {
        }
    }
}
